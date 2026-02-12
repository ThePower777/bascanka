namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for Python.  Handles keywords, triple-quoted strings (<c>"""</c> and
/// <c>'''</c>), f-strings, single-line strings, comments (<c>#</c>), and
/// decorators (<c>@</c>).
/// </summary>
public sealed class PythonLexer : BaseLexer
{
    private const int StateInTripleDoubleString = 10;
    private const int StateInTripleSingleString = 11;

    public override string LanguageId => "python";
    public override string[] FileExtensions => [".py", ".pyi", ".pyw"];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "False", "None", "True", "and", "as", "assert", "async", "await",
        "break", "class", "continue", "def", "del", "elif", "else",
        "except", "finally", "for", "from", "global", "if", "import",
        "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise",
        "return", "try", "while", "with", "yield", "match", "case", "type",
    };

    private static readonly HashSet<string> Builtins = new(StringComparer.Ordinal)
    {
        "int", "float", "str", "bool", "list", "dict", "tuple", "set",
        "frozenset", "bytes", "bytearray", "memoryview", "range", "enumerate",
        "zip", "map", "filter", "sorted", "reversed", "len", "print",
        "input", "open", "type", "isinstance", "issubclass", "super",
        "property", "classmethod", "staticmethod", "object", "Exception",
        "ValueError", "TypeError", "KeyError", "IndexError", "AttributeError",
        "RuntimeError", "StopIteration", "NotImplementedError", "OSError",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Comment.
        if (c == '#')
        {
            ReadLineComment(line, ref pos, tokens, 1);
            return state;
        }

        // Decorator.
        if (c == '@' && IsLineStart(line, pos))
        {
            int start = pos;
            pos++;
            while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '.'))
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Attribute));
            return state;
        }

        // String prefixes: f, r, b, u, rb, br, fr, rf (case insensitive).
        if (IsStringPrefix(line, pos, out int prefixLen))
        {
            int afterPrefix = pos + prefixLen;
            if (afterPrefix < line.Length && (line[afterPrefix] == '"' || line[afterPrefix] == '\''))
            {
                return ReadPythonString(line, ref pos, tokens, prefixLen);
            }
        }

        // Strings without prefix.
        if (c == '"' || c == '\'')
        {
            return ReadPythonString(line, ref pos, tokens, 0);
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
            int len = ReadIdentifierLength(line, pos);
            string word = line.Substring(pos, len);
            TokenType type;

            if (Keywords.Contains(word))
                type = TokenType.Keyword;
            else if (Builtins.Contains(word))
                type = TokenType.TypeName;
            else if (len > 0 && char.IsUpper(word[0]))
                type = TokenType.TypeName;
            else
                type = TokenType.Identifier;

            tokens.Add(new Token(pos, len, type));
            pos += len;
            return state;
        }

        // Operators.
        if (IsOperatorChar(c) || c == '@')
        {
            int len = 1;
            if (pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "+=" or "-=" or "*=" or
                    "/=" or "//=" or "%=" or "**" or "<<" or ">>" or "->" or ":=")
                    len = 2;
                if (pos + 2 < line.Length)
                {
                    string three = line.Substring(pos, 3);
                    if (three is "**=" or "//=" or "<<=" or ">>=")
                        len = 3;
                }
            }
            EmitOperator(line, ref pos, tokens, len);
            return state;
        }

        // Punctuation.
        if (IsPunctuation(c) || c == ']' || c == ':')
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
            StateInTripleDoubleString => ContinueTripleString(line, ref pos, tokens, "\"\"\"", StateInTripleDoubleString),
            StateInTripleSingleString => ContinueTripleString(line, ref pos, tokens, "'''", StateInTripleSingleString),
            _ => state,
        };
    }

    // ── Python strings ──────────────────────────────────────────────────

    private LexerState ReadPythonString(string line, ref int pos, List<Token> tokens, int prefixLen)
    {
        int start = pos;
        pos += prefixLen; // skip prefix

        char quote = line[pos];

        // Check for triple-quoted string.
        if (pos + 2 < line.Length && line[pos + 1] == quote && line[pos + 2] == quote)
        {
            string tripleQuote = new(quote, 3);
            pos += 3;
            int stateId = quote == '"' ? StateInTripleDoubleString : StateInTripleSingleString;

            // Search for closing triple quote on this line.
            while (pos < line.Length)
            {
                if (line[pos] == '\\' && pos + 1 < line.Length)
                {
                    pos += 2;
                }
                else if (pos + 2 < line.Length &&
                         line[pos] == quote && line[pos + 1] == quote && line[pos + 2] == quote)
                {
                    pos += 3;
                    tokens.Add(new Token(start, pos - start, TokenType.String));
                    return LexerState.Normal;
                }
                else
                {
                    pos++;
                }
            }

            tokens.Add(new Token(start, pos - start, TokenType.String));
            return new LexerState(stateId, 0);
        }

        // Single-line string.
        pos++; // skip opening quote
        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (line[pos] == quote)
            {
                pos++;
                break;
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return LexerState.Normal;
    }

    private static LexerState ContinueTripleString(
        string line, ref int pos, List<Token> tokens, string tripleQuote, int stateId)
    {
        int start = pos;
        char quote = tripleQuote[0];

        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (pos + 2 < line.Length &&
                     line[pos] == quote && line[pos + 1] == quote && line[pos + 2] == quote)
            {
                pos += 3;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(stateId, 0);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static bool IsStringPrefix(string line, int pos, out int prefixLen)
    {
        prefixLen = 0;
        if (pos >= line.Length) return false;

        char c = char.ToLower(line[pos]);

        // Two-character prefixes: rb, br, fr, rf.
        if (pos + 1 < line.Length)
        {
            char c2 = char.ToLower(line[pos + 1]);
            if ((c == 'r' && c2 == 'b') || (c == 'b' && c2 == 'r') ||
                (c == 'f' && c2 == 'r') || (c == 'r' && c2 == 'f'))
            {
                if (pos + 2 < line.Length && (line[pos + 2] == '"' || line[pos + 2] == '\''))
                {
                    prefixLen = 2;
                    return true;
                }
            }
        }

        // Single-character prefixes: f, r, b, u.
        if (c == 'f' || c == 'r' || c == 'b' || c == 'u')
        {
            if (pos + 1 < line.Length && (line[pos + 1] == '"' || line[pos + 1] == '\''))
            {
                prefixLen = 1;
                return true;
            }
        }

        return false;
    }

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
