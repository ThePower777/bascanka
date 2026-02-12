namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for C#.  Handles keywords, verbatim strings (<c>@""</c>),
/// interpolated strings (<c>$""</c>), raw string literals (<c>"""</c>),
/// character literals, comments (<c>//</c>, <c>/* */</c>), preprocessor
/// directives, and attributes.
/// </summary>
public sealed class CSharpLexer : BaseLexer
{
    private const int StateInVerbatimString = 10;
    private const int StateInRawString = 11;
    private const int StateInInterpolatedVerbatim = 12;

    public override string LanguageId => "csharp";
    public override string[] FileExtensions => [".cs", ".csx"];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
        "lock", "long", "namespace", "new", "null", "object", "operator",
        "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "var", "virtual", "void", "volatile",
        "while", "yield", "async", "await", "dynamic", "nameof", "when",
        "where", "from", "select", "group", "into", "orderby", "join",
        "let", "on", "equals", "by", "ascending", "descending", "record",
        "init", "with", "required", "file", "scoped", "global", "not",
        "and", "or", "managed", "unmanaged", "nint", "nuint",
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal)
    {
        "Boolean", "Byte", "Char", "DateTime", "Decimal", "Double", "Guid",
        "Int16", "Int32", "Int64", "Object", "SByte", "Single", "String",
        "TimeSpan", "UInt16", "UInt32", "UInt64", "Void", "Task", "ValueTask",
        "List", "Dictionary", "HashSet", "IEnumerable", "IList", "ICollection",
        "IReadOnlyList", "IReadOnlyCollection", "Span", "ReadOnlySpan", "Memory",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Preprocessor directives.
        if (c == '#' && IsLineStart(line, pos))
        {
            ReadLineComment(line, ref pos, tokens, 1);
            tokens[^1] = new Token(tokens[^1].Start, tokens[^1].Length, TokenType.Preprocessor);
            return state;
        }

        // Attributes.
        if (c == '[')
        {
            // Check for common attribute patterns like [Serializable], [Test], etc.
            // Just emit as punctuation -- the contents will be handled by normal tokenization.
            EmitPunctuation(line, ref pos, tokens);
            return state;
        }

        // Single-line comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '/')
        {
            ReadLineComment(line, ref pos, tokens);
            return state;
        }

        // Multi-line comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }

        // Raw string literal: """ ... """
        if (c == '"' && pos + 2 < line.Length && line[pos + 1] == '"' && line[pos + 2] == '"')
        {
            return ReadRawStringLiteral(line, ref pos, tokens);
        }

        // Verbatim string: @""
        if (c == '@' && pos + 1 < line.Length && line[pos + 1] == '"')
        {
            return ReadVerbatimString(line, ref pos, tokens);
        }

        // Interpolated verbatim: $@"" or @$""
        if ((c == '$' && pos + 2 < line.Length && line[pos + 1] == '@' && line[pos + 2] == '"') ||
            (c == '@' && pos + 2 < line.Length && line[pos + 1] == '$' && line[pos + 2] == '"'))
        {
            return ReadInterpolatedVerbatimString(line, ref pos, tokens);
        }

        // Interpolated string: $""
        if (c == '$' && pos + 1 < line.Length && line[pos + 1] == '"')
        {
            ReadString(line, ref pos, tokens, '"');
            // Adjust: include the $ prefix.
            return state;
        }

        // Regular string.
        if (c == '"')
        {
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
            // Two-char operators.
            if (pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "=>" or "++" or "--" or "+=" or "-=" or "*=" or "/=" or
                    "%=" or "&=" or "|=" or "^=" or "<<" or ">>" or "??" or "?.")
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

        // Fallback.
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
            StateInVerbatimString => ContinueVerbatimString(line, ref pos, tokens),
            StateInRawString => ContinueRawString(line, ref pos, tokens, state.NestingDepth),
            StateInInterpolatedVerbatim => ContinueVerbatimString(line, ref pos, tokens),
            _ => state,
        };
    }

    // ── Verbatim strings ────────────────────────────────────────────────

    private static LexerState ReadVerbatimString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos += 2; // skip @"

        while (pos < line.Length)
        {
            if (line[pos] == '"')
            {
                if (pos + 1 < line.Length && line[pos + 1] == '"')
                {
                    pos += 2; // escaped double-quote
                }
                else
                {
                    pos++; // closing quote
                    tokens.Add(new Token(start, pos - start, TokenType.String));
                    return LexerState.Normal;
                }
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(StateInVerbatimString, 0);
    }

    private static LexerState ContinueVerbatimString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;

        while (pos < line.Length)
        {
            if (line[pos] == '"')
            {
                if (pos + 1 < line.Length && line[pos + 1] == '"')
                {
                    pos += 2;
                }
                else
                {
                    pos++;
                    tokens.Add(new Token(start, pos - start, TokenType.String));
                    return LexerState.Normal;
                }
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(StateInVerbatimString, 0);
    }

    // ── Interpolated verbatim strings ───────────────────────────────────

    private static LexerState ReadInterpolatedVerbatimString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos += 3; // skip $@" or @$"

        while (pos < line.Length)
        {
            if (line[pos] == '"')
            {
                if (pos + 1 < line.Length && line[pos + 1] == '"')
                {
                    pos += 2;
                }
                else
                {
                    pos++;
                    tokens.Add(new Token(start, pos - start, TokenType.String));
                    return LexerState.Normal;
                }
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(StateInInterpolatedVerbatim, 0);
    }

    // ── Raw string literals ─────────────────────────────────────────────

    private LexerState ReadRawStringLiteral(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;

        // Count the opening quotes.
        int quoteCount = 0;
        while (pos < line.Length && line[pos] == '"')
        {
            quoteCount++;
            pos++;
        }

        // Look for the closing sequence of the same number of quotes.
        string closing = new('"', quoteCount);
        int closeIdx = line.IndexOf(closing, pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + quoteCount;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return LexerState.Normal;
        }

        // Multi-line.
        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        pos = line.Length;
        return new LexerState(StateInRawString, quoteCount);
    }

    private static LexerState ContinueRawString(string line, ref int pos, List<Token> tokens, int quoteCount)
    {
        int start = pos;
        string closing = new('"', quoteCount);
        int closeIdx = line.IndexOf(closing, pos, StringComparison.Ordinal);

        if (closeIdx >= 0)
        {
            pos = closeIdx + quoteCount;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        pos = line.Length;
        return new LexerState(StateInRawString, quoteCount);
    }

    // ── Character literals ──────────────────────────────────────────────

    private static void ReadCharLiteral(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip opening '

        if (pos < line.Length && line[pos] == '\\' && pos + 1 < line.Length)
        {
            pos += 2; // escape sequence
        }
        else if (pos < line.Length)
        {
            pos++; // the character
        }

        if (pos < line.Length && line[pos] == '\'')
        {
            pos++; // closing '
        }

        tokens.Add(new Token(start, pos - start, TokenType.Character));
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static bool IsLineStart(string line, int pos)
    {
        for (int i = 0; i < pos; i++)
        {
            if (!char.IsWhiteSpace(line[i]))
                return false;
        }
        return true;
    }
}
