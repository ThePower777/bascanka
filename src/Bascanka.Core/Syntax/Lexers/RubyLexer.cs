namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for Ruby.  Handles keywords, symbols (<c>:sym</c>), strings (single,
/// double, heredocs), comments (<c>#</c>), regex literals (<c>/pattern/</c>),
/// and <c>=begin</c>/<c>=end</c> block comments.
/// </summary>
public sealed class RubyLexer : BaseLexer
{
    private const int StateInDoubleString = 10;
    private const int StateInBlockComment = 11;
    private const int StateInHeredoc = 12;
    private const int StateInRegex = 13;

    public override string LanguageId => "ruby";
    public override string[] FileExtensions => [".rb", ".rake", ".gemspec", ".podspec", ".rbw"];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "BEGIN", "END", "__ENCODING__", "__END__", "__FILE__", "__LINE__",
        "alias", "and", "begin", "break", "case", "class", "def", "defined?",
        "do", "else", "elsif", "end", "ensure", "false", "for", "if", "in",
        "module", "next", "nil", "not", "or", "redo", "rescue", "retry",
        "return", "self", "super", "then", "true", "undef", "unless",
        "until", "when", "while", "yield", "raise", "require", "require_relative",
        "include", "extend", "prepend", "attr_accessor", "attr_reader",
        "attr_writer", "puts", "print", "p", "lambda", "proc",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // =begin ... =end block comment (must start at column 0).
        if (pos == 0 && StartsWith(line, 0, "=begin"))
        {
            int start = pos;
            pos = line.Length;
            tokens.Add(new Token(start, pos - start, TokenType.MultiLineComment));
            return new LexerState(StateInBlockComment, 0);
        }

        // Single-line comment.
        if (c == '#')
        {
            ReadLineComment(line, ref pos, tokens, 1);
            return state;
        }

        // Symbol.
        if (c == ':' && pos + 1 < line.Length && (IsIdentStart(line[pos + 1]) || line[pos + 1] == '"'))
        {
            int start = pos;
            pos++; // skip :
            if (line[pos] == '"')
            {
                // Quoted symbol: :"string"
                ReadString(line, ref pos, tokens, '"');
                // Rewrite the last token to include the colon.
                var last = tokens[^1];
                tokens[^1] = new Token(start, pos - start, TokenType.String);
            }
            else
            {
                while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '?' || line[pos] == '!'))
                    pos++;
                tokens.Add(new Token(start, pos - start, TokenType.String));
            }
            return state;
        }

        // Double-quoted string.
        if (c == '"')
        {
            ReadString(line, ref pos, tokens, '"');
            return state;
        }

        // Single-quoted string.
        if (c == '\'')
        {
            ReadRubySingleString(line, ref pos, tokens);
            return state;
        }

        // Regex literal.
        if (c == '/' && CanStartRegex(tokens))
        {
            return ReadRegex(line, ref pos, tokens);
        }

        // Heredoc.
        if (c == '<' && pos + 1 < line.Length && line[pos + 1] == '<')
        {
            // Simple emit as operator -- full heredoc parsing requires multi-line delimiter tracking.
            EmitOperator(line, ref pos, tokens, 2);
            if (pos < line.Length && line[pos] == '-')
                pos++; // <<- or <<~
            if (pos < line.Length && line[pos] == '~')
                pos++;
            return state;
        }

        // Numbers.
        if (char.IsDigit(c) || (c == '.' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
        {
            ReadNumber(line, ref pos, tokens);
            return state;
        }

        // Instance/class variables.
        if (c == '@')
        {
            int start = pos;
            pos++;
            if (pos < line.Length && line[pos] == '@') pos++; // @@class_var
            while (pos < line.Length && IsIdentPart(line[pos]))
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return state;
        }

        // Global variables.
        if (c == '$')
        {
            int start = pos;
            pos++;
            if (pos < line.Length && (IsIdentPart(line[pos]) || "~!@&+`'=/<>,.;:\"\\-?*$".Contains(line[pos])))
            {
                if (IsIdentStart(line[pos]))
                {
                    while (pos < line.Length && IsIdentPart(line[pos]))
                        pos++;
                }
                else
                {
                    pos++;
                }
            }
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return state;
        }

        // Identifiers, keywords, and constants.
        if (IsIdentStart(c))
        {
            int start = pos;
            while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '?' || line[pos] == '!'))
                pos++;
            string word = line.Substring(start, pos - start);
            TokenType type;
            if (Keywords.Contains(word))
                type = TokenType.Keyword;
            else if (char.IsUpper(word[0]))
                type = TokenType.TypeName;
            else
                type = TokenType.Identifier;
            tokens.Add(new Token(start, pos - start, type));
            return state;
        }

        // Percent-literal strings: %w[], %i[], %q{}, %Q{}, etc.
        if (c == '%' && pos + 1 < line.Length)
        {
            char next = line[pos + 1];
            if ("wWiIqQrxs".Contains(next) || !char.IsLetterOrDigit(next))
            {
                int start = pos;
                pos++; // skip %
                if (char.IsLetter(next)) pos++; // skip qualifier letter

                if (pos < line.Length)
                {
                    char open = line[pos];
                    char close = open switch
                    {
                        '(' => ')',
                        '[' => ']',
                        '{' => '}',
                        '<' => '>',
                        _ => open,
                    };
                    pos++; // skip opening delimiter
                    int depth = 1;
                    while (pos < line.Length && depth > 0)
                    {
                        if (line[pos] == '\\' && pos + 1 < line.Length)
                        {
                            pos += 2;
                        }
                        else if (open != close && line[pos] == open)
                        {
                            depth++;
                            pos++;
                        }
                        else if (line[pos] == close)
                        {
                            depth--;
                            if (depth > 0) pos++;
                        }
                        else
                        {
                            pos++;
                        }
                    }
                    if (pos < line.Length) pos++; // skip closing delimiter
                }
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return state;
            }
        }

        // Operators.
        if (IsOperatorChar(c) || c == '.' || c == '~')
        {
            int len = 1;
            if (pos + 2 < line.Length)
            {
                string three = line.Substring(pos, 3);
                if (three is "<=>" or "===" or "**=" or "&&=" or "||=" or "<<=" or ">>=" or "...")
                    len = 3;
            }
            if (len == 1 && pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "+=" or "-=" or "*=" or "/=" or "%=" or "**" or "=~" or
                    "!~" or ".." or "::" or "<<" or ">>" or "&=" or "|=" or "^=")
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
        if (state.StateId == StateInBlockComment)
        {
            return ContinueBlockComment(line, ref pos, tokens);
        }
        return state;
    }

    // ── Block comment =begin/=end ───────────────────────────────────────

    private static LexerState ContinueBlockComment(string line, ref int pos, List<Token> tokens)
    {
        if (line.StartsWith("=end", StringComparison.Ordinal))
        {
            tokens.Add(new Token(0, line.Length, TokenType.MultiLineComment));
            pos = line.Length;
            return LexerState.Normal;
        }

        tokens.Add(new Token(0, line.Length, TokenType.MultiLineComment));
        pos = line.Length;
        return new LexerState(StateInBlockComment, 0);
    }

    // ── Regex ───────────────────────────────────────────────────────────

    private static LexerState ReadRegex(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip /

        bool inCharClass = false;
        while (pos < line.Length)
        {
            char ch = line[pos];
            if (ch == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (ch == '[')
            {
                inCharClass = true;
                pos++;
            }
            else if (ch == ']')
            {
                inCharClass = false;
                pos++;
            }
            else if (ch == '/' && !inCharClass)
            {
                pos++;
                // Flags.
                while (pos < line.Length && char.IsLetter(line[pos]))
                    pos++;
                tokens.Add(new Token(start, pos - start, TokenType.Regex));
                return LexerState.Normal;
            }
            else
            {
                pos++;
            }
        }

        // Unterminated regex.
        tokens.Add(new Token(start, 1, TokenType.Operator));
        pos = start + 1;
        return LexerState.Normal;
    }

    private static void ReadRubySingleString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip '
        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length && (line[pos + 1] == '\'' || line[pos + 1] == '\\'))
            {
                pos += 2;
            }
            else if (line[pos] == '\'')
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
    }

    private static bool CanStartRegex(List<Token> tokens)
    {
        if (tokens.Count == 0) return true;
        TokenType last = tokens[^1].Type;
        return last is TokenType.Operator or TokenType.Punctuation or
               TokenType.Keyword or TokenType.Plain;
    }
}
