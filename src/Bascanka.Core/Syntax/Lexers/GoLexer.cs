namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for Go.  Handles keywords, raw strings (<c>`...`</c>), interpreted
/// strings (<c>"..."</c>), rune literals (<c>'...'</c>), comments (<c>//</c>
/// and <c>/* */</c>), and numeric literals.
/// </summary>
public sealed class GoLexer : BaseLexer
{
    private const int StateInRawString = 10;

    public override string LanguageId => "go";
    public override string[] FileExtensions => [".go"];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "break", "case", "chan", "const", "continue", "default", "defer",
        "else", "fallthrough", "for", "func", "go", "goto", "if", "import",
        "interface", "map", "package", "range", "return", "select", "struct",
        "switch", "type", "var", "true", "false", "nil", "iota", "append",
        "cap", "close", "copy", "delete", "len", "make", "new", "panic",
        "print", "println", "recover", "any",
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal)
    {
        "bool", "byte", "complex64", "complex128", "error", "float32",
        "float64", "int", "int8", "int16", "int32", "int64", "rune",
        "string", "uint", "uint8", "uint16", "uint32", "uint64", "uintptr",
        "comparable",
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

        // Block comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }

        // Raw string literal: `...`
        if (c == '`')
        {
            return ReadRawString(line, ref pos, tokens);
        }

        // Interpreted string.
        if (c == '"')
        {
            ReadString(line, ref pos, tokens, '"');
            return state;
        }

        // Rune literal.
        if (c == '\'')
        {
            ReadRuneLiteral(line, ref pos, tokens);
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
                if (three is "<<=" or ">>=" or "...")
                    len = 3;
            }
            if (len == 1 && pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "++" or "--" or "+=" or "-=" or "*=" or "/=" or "%=" or
                    "&=" or "|=" or "^=" or "<<" or ">>" or ":=" or "<-" or
                    "&^")
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
            StateInRawString => ContinueRawString(line, ref pos, tokens),
            _ => state,
        };
    }

    // ── Raw strings ─────────────────────────────────────────────────────

    private static LexerState ReadRawString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip `

        while (pos < line.Length)
        {
            if (line[pos] == '`')
            {
                pos++;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            pos++;
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(StateInRawString, 0);
    }

    private static LexerState ContinueRawString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;

        while (pos < line.Length)
        {
            if (line[pos] == '`')
            {
                pos++;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            pos++;
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(StateInRawString, 0);
    }

    // ── Rune literal ────────────────────────────────────────────────────

    private static void ReadRuneLiteral(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip '

        if (pos < line.Length && line[pos] == '\\')
        {
            pos++; // skip backslash
            // Handle \x, \u, \U, \ooo, etc.
            if (pos < line.Length)
            {
                char esc = line[pos];
                pos++;
                if (esc == 'x')
                {
                    while (pos < line.Length && IsHexDigit(line[pos])) pos++;
                }
                else if (esc == 'u')
                {
                    for (int i = 0; i < 4 && pos < line.Length && IsHexDigit(line[pos]); i++) pos++;
                }
                else if (esc == 'U')
                {
                    for (int i = 0; i < 8 && pos < line.Length && IsHexDigit(line[pos]); i++) pos++;
                }
            }
        }
        else if (pos < line.Length)
        {
            pos++;
        }

        if (pos < line.Length && line[pos] == '\'')
            pos++;

        tokens.Add(new Token(start, pos - start, TokenType.Character));
    }
}
