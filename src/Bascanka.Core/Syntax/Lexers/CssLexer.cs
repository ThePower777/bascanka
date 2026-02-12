namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for CSS.  Handles selectors, properties, values, colour literals,
/// <c>/* */</c> comments, and <c>@</c>-rules.
/// </summary>
public sealed class CssLexer : BaseLexer
{
    private const int StateInProperty = 10;

    public override string LanguageId => "css";
    public override string[] FileExtensions => [".css", ".scss", ".sass", ".less"];

    private static readonly HashSet<string> AtRules = new(StringComparer.OrdinalIgnoreCase)
    {
        "@import", "@media", "@font-face", "@keyframes", "@charset",
        "@namespace", "@supports", "@page", "@layer", "@container",
        "@property", "@scope", "@starting-style",
    };

    private static readonly HashSet<string> ValueKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "inherit", "initial", "unset", "revert", "revert-layer",
        "none", "auto", "normal", "bold", "italic", "solid", "dashed",
        "dotted", "hidden", "visible", "block", "inline", "flex", "grid",
        "absolute", "relative", "fixed", "sticky", "static",
        "transparent", "currentColor", "important",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Block comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }

        // Single-line comment (SCSS/Less).
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '/')
        {
            ReadLineComment(line, ref pos, tokens);
            return state;
        }

        // @-rules.
        if (c == '@')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '-'))
                pos++;
            string word = line.Substring(start, pos - start);
            TokenType type = AtRules.Contains(word) ? TokenType.Keyword : TokenType.Keyword;
            tokens.Add(new Token(start, pos - start, type));
            return state;
        }

        // Strings.
        if (c == '"' || c == '\'')
        {
            ReadString(line, ref pos, tokens, c);
            return state;
        }

        // Colour hex literal.
        if (c == '#')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && IsHexDigit(line[pos]))
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Number));
            return state;
        }

        // Numbers (including units).
        if (char.IsDigit(c) || (c == '.' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
        {
            int start = pos;
            ReadNumber(line, ref pos, tokens);
            // Read unit suffix (px, em, rem, %, vh, vw, etc.).
            while (pos < line.Length && (char.IsLetter(line[pos]) || line[pos] == '%'))
                pos++;
            // Replace the number token with one that includes the unit.
            tokens[^1] = new Token(start, pos - start, TokenType.Number);
            return state;
        }

        // Identifiers: properties, selectors, values.
        if (IsIdentStart(c) || c == '-')
        {
            int start = pos;
            if (c == '-')
            {
                // CSS custom property or vendor prefix.
                pos++;
            }
            while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '-'))
                pos++;
            string word = line.Substring(start, pos - start);

            TokenType type;
            if (ValueKeywords.Contains(word))
                type = TokenType.Keyword;
            else
                type = TokenType.Identifier;

            tokens.Add(new Token(start, pos - start, type));
            return state;
        }

        // Punctuation.
        if (c == '{' || c == '}' || c == ';' || c == ',' || c == '(' || c == ')' ||
            c == '[' || c == ']')
        {
            EmitPunctuation(line, ref pos, tokens);
            return state;
        }

        // Colon.
        if (c == ':')
        {
            EmitPunctuation(line, ref pos, tokens);
            return state;
        }

        // Operators.
        if (c == '>' || c == '+' || c == '~' || c == '*' || c == '=' || c == '!' || c == '|')
        {
            EmitOperator(line, ref pos, tokens);
            return state;
        }

        // Dot and class selectors.
        if (c == '.')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '-'))
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return state;
        }

        tokens.Add(new Token(pos, 1, TokenType.Plain));
        pos++;
        return state;
    }

    protected override LexerState ContinueMultiLineState(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (state.StateId == LexerState.StateInMultiLineComment)
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }
        return state;
    }
}
