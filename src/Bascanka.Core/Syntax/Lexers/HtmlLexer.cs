namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for HTML.  Handles tags, attributes, attribute values, entities
/// (<c>&amp;amp;</c>), and HTML comments (<c>&lt;!-- --&gt;</c>).
/// Does not attempt to lex embedded <c>&lt;script&gt;</c> or <c>&lt;style&gt;</c>
/// content with their respective lexers; those regions are emitted as plain text.
/// </summary>
public sealed class HtmlLexer : BaseLexer
{
    private const int StateInComment = 10;

    public override string LanguageId => "html";
    public override string[] FileExtensions => [".html", ".htm", ".xhtml", ".shtml"];

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // HTML comment start.
        if (StartsWith(line, pos, "<!--"))
        {
            return ReadHtmlComment(line, ref pos, tokens);
        }

        // DOCTYPE.
        if (StartsWith(line, pos, "<!"))
        {
            int start = pos;
            pos += 2;
            while (pos < line.Length && line[pos] != '>')
                pos++;
            if (pos < line.Length) pos++; // skip >
            tokens.Add(new Token(start, pos - start, TokenType.Tag));
            return state;
        }

        // Tag open: < or </
        if (c == '<')
        {
            return ReadTag(line, ref pos, tokens);
        }

        // Entity.
        if (c == '&')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && line[pos] != ';' && !char.IsWhiteSpace(line[pos]) && (pos - start) < 12)
                pos++;
            if (pos < line.Length && line[pos] == ';')
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Entity));
            return state;
        }

        // Plain text between tags.
        int textStart = pos;
        while (pos < line.Length && line[pos] != '<' && line[pos] != '&')
            pos++;

        if (pos > textStart)
            tokens.Add(new Token(textStart, pos - textStart, TokenType.Plain));

        return state;
    }

    protected override LexerState ContinueMultiLineState(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        return state.StateId switch
        {
            StateInComment => ContinueHtmlComment(line, ref pos, tokens),
            LexerState.StateInTag => ContinueTag(line, ref pos, tokens),
            _ => state,
        };
    }

    // ── HTML comments ───────────────────────────────────────────────────

    private static LexerState ReadHtmlComment(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos += 4; // skip <!--

        int closeIdx = line.IndexOf("-->", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 3;
            tokens.Add(new Token(start, pos - start, TokenType.MultiLineComment));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.MultiLineComment));
        pos = line.Length;
        return new LexerState(StateInComment, 0);
    }

    private static LexerState ContinueHtmlComment(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        int closeIdx = line.IndexOf("-->", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 3;
            tokens.Add(new Token(start, pos - start, TokenType.MultiLineComment));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.MultiLineComment));
        pos = line.Length;
        return new LexerState(StateInComment, 0);
    }

    // ── Tags ────────────────────────────────────────────────────────────

    private static LexerState ReadTag(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip <

        // Closing tag?
        if (pos < line.Length && line[pos] == '/')
            pos++;

        // Read tag name.
        int nameStart = pos;
        while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '-' || line[pos] == ':'))
            pos++;

        tokens.Add(new Token(start, pos - start, TokenType.Tag));

        // Read attributes until > or end of line.
        return ReadAttributes(line, ref pos, tokens);
    }

    private static LexerState ReadAttributes(string line, ref int pos, List<Token> tokens)
    {
        while (pos < line.Length)
        {
            // Skip whitespace.
            if (char.IsWhiteSpace(line[pos]))
            {
                int ws = pos;
                while (pos < line.Length && char.IsWhiteSpace(line[pos]))
                    pos++;
                tokens.Add(new Token(ws, pos - ws, TokenType.Plain));
                continue;
            }

            // Self-closing or closing.
            if (line[pos] == '/' && pos + 1 < line.Length && line[pos + 1] == '>')
            {
                EmitPunctuation(line, ref pos, tokens, 2);
                return LexerState.Normal;
            }

            if (line[pos] == '>')
            {
                EmitPunctuation(line, ref pos, tokens);
                return LexerState.Normal;
            }

            // Attribute name.
            if (IsIdentStart(line[pos]) || line[pos] == '-' || line[pos] == ':' || line[pos] == '@' || line[pos] == '*')
            {
                int attrStart = pos;
                while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '-' || line[pos] == ':'))
                    pos++;
                tokens.Add(new Token(attrStart, pos - attrStart, TokenType.TagAttribute));

                // Skip = and optional whitespace.
                if (pos < line.Length && line[pos] == '=')
                {
                    EmitOperator(line, ref pos, tokens);

                    while (pos < line.Length && char.IsWhiteSpace(line[pos]))
                        pos++;

                    // Attribute value.
                    if (pos < line.Length && (line[pos] == '"' || line[pos] == '\''))
                    {
                        ReadAttrValue(line, ref pos, tokens, line[pos]);
                    }
                    else if (pos < line.Length)
                    {
                        // Unquoted value.
                        int valStart = pos;
                        while (pos < line.Length && !char.IsWhiteSpace(line[pos]) && line[pos] != '>')
                            pos++;
                        tokens.Add(new Token(valStart, pos - valStart, TokenType.TagAttributeValue));
                    }
                }
                continue;
            }

            // Unknown character in tag context.
            tokens.Add(new Token(pos, 1, TokenType.Plain));
            pos++;
        }

        // Tag not closed on this line.
        return new LexerState(LexerState.StateInTag, 0);
    }

    private static LexerState ContinueTag(string line, ref int pos, List<Token> tokens)
    {
        return ReadAttributes(line, ref pos, tokens);
    }

    private static void ReadAttrValue(string line, ref int pos, List<Token> tokens, char quote)
    {
        int start = pos;
        pos++; // skip opening quote

        while (pos < line.Length && line[pos] != quote)
            pos++;

        if (pos < line.Length)
            pos++; // skip closing quote

        tokens.Add(new Token(start, pos - start, TokenType.TagAttributeValue));
    }
}
