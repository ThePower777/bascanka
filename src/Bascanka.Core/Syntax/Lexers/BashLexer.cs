namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for Bash/Shell scripts.  Handles keywords, single-quoted strings
/// (no interpolation), double-quoted strings (with <c>$</c> interpolation),
/// comments (<c>#</c>), variables (<c>$VAR</c>, <c>${VAR}</c>), and here-docs.
/// </summary>
public sealed class BashLexer : BaseLexer
{
    private const int StateInDoubleString = 10;
    private const int StateInHereDoc = 11;

    public override string LanguageId => "bash";
    public override string[] FileExtensions => [".sh", ".bash", ".zsh", ".ksh", ".fish"];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "then", "else", "elif", "fi", "case", "esac", "for", "while",
        "until", "do", "done", "in", "function", "select", "time", "coproc",
        "break", "continue", "return", "exit", "export", "readonly",
        "declare", "local", "typeset", "unset", "shift", "trap", "eval",
        "exec", "source", "alias", "unalias", "set", "shopt", "true",
        "false", "test", "read", "echo", "printf", "let",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Comment.
        if (c == '#' && !IsAfterDollar(tokens))
        {
            ReadLineComment(line, ref pos, tokens, 1);
            return state;
        }

        // Here-doc start: << or <<-
        if (c == '<' && pos + 1 < line.Length && line[pos + 1] == '<')
        {
            // We just emit the operator; actual here-doc body tracking would
            // need the delimiter word. Simplified: emit as operator.
            int len = 2;
            if (pos + 2 < line.Length && line[pos + 2] == '-')
                len = 3;
            EmitOperator(line, ref pos, tokens, len);
            return state;
        }

        // Single-quoted string: no interpolation, no escapes.
        if (c == '\'')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && line[pos] != '\'')
                pos++;
            if (pos < line.Length) pos++;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return state;
        }

        // Double-quoted string.
        if (c == '"')
        {
            return ReadDoubleString(line, ref pos, tokens);
        }

        // Back-tick command substitution.
        if (c == '`')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && line[pos] != '`')
            {
                if (line[pos] == '\\' && pos + 1 < line.Length)
                    pos += 2;
                else
                    pos++;
            }
            if (pos < line.Length) pos++;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return state;
        }

        // Variable.
        if (c == '$')
        {
            ReadVariable(line, ref pos, tokens);
            return state;
        }

        // Numbers.
        if (char.IsDigit(c))
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
            tokens.Add(new Token(pos, len, type));
            pos += len;
            return state;
        }

        // Operators.
        if (c == '|' || c == '&' || c == '>' || c == '<' || c == '!' ||
            c == '=' || c == '-' || c == '+')
        {
            int len = 1;
            if (pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "||" or "&&" or ">>" or "<<" or ">=" or "<=" or
                    "!=" or "==" or "|&" or "&>" or "2>" or ">&")
                    len = 2;
            }
            EmitOperator(line, ref pos, tokens, len);
            return state;
        }

        // Punctuation.
        if (c == '(' || c == ')' || c == '{' || c == '}' || c == '[' || c == ']' ||
            c == ';' || c == ',')
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
        if (state.StateId == StateInDoubleString)
        {
            return ContinueDoubleString(line, ref pos, tokens);
        }
        return state;
    }

    // ── Double-quoted strings ───────────────────────────────────────────

    private static LexerState ReadDoubleString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip opening "

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

    private static LexerState ContinueDoubleString(string line, ref int pos, List<Token> tokens)
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

    // ── Variables ───────────────────────────────────────────────────────

    private static void ReadVariable(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip $

        if (pos >= line.Length)
        {
            tokens.Add(new Token(start, 1, TokenType.Operator));
            return;
        }

        char next = line[pos];

        // ${...}
        if (next == '{')
        {
            pos++;
            int depth = 1;
            while (pos < line.Length && depth > 0)
            {
                if (line[pos] == '{') depth++;
                else if (line[pos] == '}') depth--;
                if (depth > 0) pos++;
            }
            if (pos < line.Length) pos++; // skip closing }
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return;
        }

        // $(...)  command substitution
        if (next == '(')
        {
            pos++;
            int depth = 1;
            while (pos < line.Length && depth > 0)
            {
                if (line[pos] == '(') depth++;
                else if (line[pos] == ')') depth--;
                if (depth > 0) pos++;
            }
            if (pos < line.Length) pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return;
        }

        // Special variables: $?, $!, $$, $#, $@, $*, $0-$9.
        if ("?!$#@*-".Contains(next) || char.IsDigit(next))
        {
            pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return;
        }

        // Regular variable: $IDENT.
        while (pos < line.Length && (IsIdentPart(line[pos])))
            pos++;

        tokens.Add(new Token(start, pos - start, TokenType.Identifier));
    }

    private static bool IsAfterDollar(List<Token> tokens)
    {
        // Heuristic to avoid treating # in ${#var} as a comment.
        return false;
    }
}
