namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for Java.  Handles keywords, annotations (<c>@Override</c>),
/// strings, character literals, comments (<c>//</c> and <c>/* */</c>),
/// and numeric literals.
/// </summary>
public sealed class JavaLexer : BaseLexer
{
    public override string LanguageId => "java";
    public override string[] FileExtensions => [".java"];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "assert", "boolean", "break", "byte", "case", "catch",
        "char", "class", "const", "continue", "default", "do", "double",
        "else", "enum", "extends", "false", "final", "finally", "float",
        "for", "goto", "if", "implements", "import", "instanceof", "int",
        "interface", "long", "native", "new", "null", "package", "private",
        "protected", "public", "return", "short", "static", "strictfp",
        "super", "switch", "synchronized", "this", "throw", "throws",
        "transient", "true", "try", "void", "volatile", "while",
        "var", "yield", "record", "sealed", "permits", "non-sealed",
        "when", "module", "requires", "exports", "opens", "uses",
        "provides", "with", "to", "transitive",
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal)
    {
        "Boolean", "Byte", "Character", "Class", "Comparable", "Double",
        "Enum", "Exception", "Float", "Integer", "Iterable", "Long",
        "Math", "Number", "Object", "Optional", "Override", "Runnable",
        "Runtime", "Short", "String", "StringBuilder", "StringBuffer",
        "System", "Thread", "Throwable", "Void",
        "ArrayList", "HashMap", "HashSet", "LinkedList", "List", "Map",
        "Set", "Collection", "Collections", "Arrays", "Iterator",
        "Stream", "Collectors", "CompletableFuture", "Future",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Single-line comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '/')
        {
            ReadLineComment(line, ref pos, tokens);
            return state;
        }

        // Block comment or Javadoc.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }

        // Annotation.
        if (c == '@')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '.'))
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Attribute));
            return state;
        }

        // Strings.
        if (c == '"')
        {
            // Text block (Java 13+): """
            if (pos + 2 < line.Length && line[pos + 1] == '"' && line[pos + 2] == '"')
            {
                return ReadTextBlock(line, ref pos, tokens);
            }
            ReadString(line, ref pos, tokens, '"');
            return state;
        }

        // Character literal.
        if (c == '\'')
        {
            ReadCharLiteral(line, ref pos, tokens);
            return state;
        }

        // Numbers.
        if (char.IsDigit(c) || (c == '.' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
        {
            ReadNumber(line, ref pos, tokens);
            return state;
        }

        // Identifiers and keywords.
        if (IsIdentStart(c))
        {
            ReadIdentifierOrKeyword(line, ref pos, tokens, Keywords, TypeNames);
            return state;
        }

        // Operators.
        if (IsOperatorChar(c))
        {
            int len = 1;
            if (pos + 2 < line.Length)
            {
                string three = line.Substring(pos, 3);
                if (three is ">>>" or "<<=" or ">>=")
                    len = 3;
            }
            if (len == 1 && pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "++" or "--" or "+=" or "-=" or "*=" or "/=" or "%=" or
                    "&=" or "|=" or "^=" or "<<" or ">>" or "->" or "::")
                    len = 2;
            }
            EmitOperator(line, ref pos, tokens, len);
            return state;
        }

        // Punctuation.
        if (IsPunctuation(c) || c == ']')
        {
            EmitPunctuation(line, ref pos, tokens);
            return state;
        }

        tokens.Add(new Token(pos, 1, TokenType.Plain));
        pos++;
        return state;
    }

    protected override LexerState ContinueMultiLineState(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        return state.StateId switch
        {
            LexerState.StateInMultiLineComment => ReadBlockComment(line, ref pos, tokens, state),
            LexerState.StateInMultiLineString => ContinueTextBlock(line, ref pos, tokens),
            _ => state,
        };
    }

    private static void ReadCharLiteral(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip '
        if (pos < line.Length && line[pos] == '\\')
        {
            pos += 2;
        }
        else if (pos < line.Length)
        {
            pos++;
        }
        if (pos < line.Length && line[pos] == '\'')
            pos++;
        tokens.Add(new Token(start, pos - start, TokenType.Character));
    }

    private static LexerState ReadTextBlock(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos += 3; // skip """

        // Search for closing """ on this line.
        int closeIdx = line.IndexOf("\"\"\"", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 3;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        pos = line.Length;
        return new LexerState(LexerState.StateInMultiLineString, 0);
    }

    private static LexerState ContinueTextBlock(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        int closeIdx = line.IndexOf("\"\"\"", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 3;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        pos = line.Length;
        return new LexerState(LexerState.StateInMultiLineString, 0);
    }
}
