namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for PHP.  Handles keywords, variables (<c>$var</c>), strings
/// (single and double quoted), comments (<c>//</c>, <c>#</c>, <c>/* */</c>),
/// and PHP tags (<c>&lt;?php</c>, <c>?&gt;</c>).
/// </summary>
public sealed class PhpLexer : BaseLexer
{
    private const int StateInDoubleString = 10;
    private const int StateInHeredoc = 11;

    public override string LanguageId => "php";
    public override string[] FileExtensions => [".php", ".phtml", ".php3", ".php4", ".php5", ".phps"];

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "and", "array", "as", "break", "callable", "case",
        "catch", "class", "clone", "const", "continue", "declare",
        "default", "do", "else", "elseif", "empty", "enddeclare",
        "endfor", "endforeach", "endif", "endswitch", "endwhile", "enum",
        "eval", "exit", "extends", "false", "final", "finally", "fn",
        "for", "foreach", "function", "global", "goto", "if",
        "implements", "include", "include_once", "instanceof", "insteadof",
        "interface", "isset", "list", "match", "mixed", "namespace",
        "new", "null", "or", "print", "private", "protected", "public",
        "readonly", "require", "require_once", "return", "self", "static",
        "switch", "throw", "trait", "true", "try", "unset", "use", "var",
        "void", "while", "xor", "yield", "yield from", "never", "fiber",
        "parent", "int", "float", "string", "bool", "object", "iterable",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // PHP open/close tags.
        if (StartsWith(line, pos, "<?php") || StartsWith(line, pos, "<?=") || StartsWith(line, pos, "<?"))
        {
            int start = pos;
            int len = StartsWith(line, pos, "<?php") ? 5
                    : StartsWith(line, pos, "<?=") ? 3 : 2;
            pos += len;
            tokens.Add(new Token(start, len, TokenType.Tag));
            return state;
        }

        if (StartsWith(line, pos, "?>"))
        {
            tokens.Add(new Token(pos, 2, TokenType.Tag));
            pos += 2;
            return state;
        }

        // Single-line comment: // or #.
        if ((c == '/' && pos + 1 < line.Length && line[pos + 1] == '/') ||
            c == '#')
        {
            int leader = c == '#' ? 1 : 2;
            ReadLineComment(line, ref pos, tokens, leader);
            return state;
        }

        // Block comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }

        // Variable.
        if (c == '$')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && IsIdentPart(line[pos]))
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return state;
        }

        // Double-quoted string.
        if (c == '"')
        {
            return ReadPhpDoubleString(line, ref pos, tokens);
        }

        // Single-quoted string.
        if (c == '\'')
        {
            ReadPhpSingleString(line, ref pos, tokens);
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
            int len = ReadIdentifierLength(line, pos);
            string word = line.Substring(pos, len);
            TokenType type = Keywords.Contains(word) ? TokenType.Keyword : TokenType.Identifier;

            // PascalCase heuristic for type names.
            if (type == TokenType.Identifier && len > 0 && char.IsUpper(word[0]))
                type = TokenType.TypeName;

            tokens.Add(new Token(pos, len, type));
            pos += len;
            return state;
        }

        // Operators.
        if (IsOperatorChar(c) || c == '.')
        {
            int len = 1;
            if (pos + 2 < line.Length)
            {
                string three = line.Substring(pos, 3);
                if (three is "===" or "!==" or "<=>" or "**=" or "??=" or "...")
                    len = 3;
            }
            if (len == 1 && pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "++" or "--" or "+=" or "-=" or "*=" or "/=" or "%=" or
                    ".=" or "=>" or "->" or "::" or "**" or "??" or "?:")
                    len = 2;
            }
            EmitOperator(line, ref pos, tokens, len);
            return state;
        }

        // Punctuation.
        if (IsPunctuation(c) || c == ']' || c == '@')
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
            StateInDoubleString => ContinuePhpDoubleString(line, ref pos, tokens),
            _ => state,
        };
    }

    // ── Strings ─────────────────────────────────────────────────────────

    private static LexerState ReadPhpDoubleString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip "

        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (line[pos] == '"')
            {
                pos++;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(StateInDoubleString, 0);
    }

    private static LexerState ContinuePhpDoubleString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;

        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (line[pos] == '"')
            {
                pos++;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(StateInDoubleString, 0);
    }

    private static void ReadPhpSingleString(string line, ref int pos, List<Token> tokens)
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
}
