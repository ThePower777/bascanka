namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for XML.  Handles tags, attributes, attribute values, CDATA sections,
/// comments (<c>&lt;!-- --&gt;</c>), and processing instructions
/// (<c>&lt;?xml ... ?&gt;</c>).
/// </summary>
public sealed class XmlLexer : BaseLexer
{
    private const int StateInComment = 10;
    private const int StateInCData = 11;
    private const int StateInTag = 12;
    private const int StateInPI = 13;

    public override string LanguageId => "xml";
    public override string[] FileExtensions => [".xml", ".xsl", ".xslt", ".xsd", ".svg", ".xaml", ".csproj", ".fsproj", ".vbproj", ".props", ".targets"];

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Comment.
        if (StartsWith(line, pos, "<!--"))
        {
            return ReadXmlComment(line, ref pos, tokens);
        }

        // CDATA section.
        if (StartsWith(line, pos, "<![CDATA["))
        {
            return ReadCData(line, ref pos, tokens);
        }

        // Processing instruction.
        if (StartsWith(line, pos, "<?"))
        {
            return ReadProcessingInstruction(line, ref pos, tokens);
        }

        // Tag.
        if (c == '<')
        {
            return ReadXmlTag(line, ref pos, tokens);
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
            StateInComment => ContinueXmlComment(line, ref pos, tokens),
            StateInCData => ContinueCData(line, ref pos, tokens),
            StateInTag => ContinueXmlTag(line, ref pos, tokens),
            StateInPI => ContinuePI(line, ref pos, tokens),
            _ => state,
        };
    }

    // ── Comments ────────────────────────────────────────────────────────

    private static LexerState ReadXmlComment(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos += 4;

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

    private static LexerState ContinueXmlComment(string line, ref int pos, List<Token> tokens)
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

    // ── CDATA ───────────────────────────────────────────────────────────

    private static LexerState ReadCData(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos += 9; // skip <![CDATA[

        int closeIdx = line.IndexOf("]]>", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 3;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        pos = line.Length;
        return new LexerState(StateInCData, 0);
    }

    private static LexerState ContinueCData(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        int closeIdx = line.IndexOf("]]>", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 3;
            tokens.Add(new Token(start, pos - start, TokenType.String));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        pos = line.Length;
        return new LexerState(StateInCData, 0);
    }

    // ── Processing instructions ─────────────────────────────────────────

    private static LexerState ReadProcessingInstruction(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos += 2; // skip <?

        int closeIdx = line.IndexOf("?>", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 2;
            tokens.Add(new Token(start, pos - start, TokenType.Preprocessor));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.Preprocessor));
        pos = line.Length;
        return new LexerState(StateInPI, 0);
    }

    private static LexerState ContinuePI(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        int closeIdx = line.IndexOf("?>", pos, StringComparison.Ordinal);
        if (closeIdx >= 0)
        {
            pos = closeIdx + 2;
            tokens.Add(new Token(start, pos - start, TokenType.Preprocessor));
            return LexerState.Normal;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.Preprocessor));
        pos = line.Length;
        return new LexerState(StateInPI, 0);
    }

    // ── Tags ────────────────────────────────────────────────────────────

    private static LexerState ReadXmlTag(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip <

        if (pos < line.Length && line[pos] == '/')
            pos++;

        // Tag name.
        int nameStart = pos;
        while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '-' || line[pos] == ':' || line[pos] == '.'))
            pos++;

        tokens.Add(new Token(start, pos - start, TokenType.Tag));

        return ReadXmlAttributes(line, ref pos, tokens);
    }

    private static LexerState ReadXmlAttributes(string line, ref int pos, List<Token> tokens)
    {
        while (pos < line.Length)
        {
            if (char.IsWhiteSpace(line[pos]))
            {
                int ws = pos;
                while (pos < line.Length && char.IsWhiteSpace(line[pos]))
                    pos++;
                tokens.Add(new Token(ws, pos - ws, TokenType.Plain));
                continue;
            }

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

            if (line[pos] == '?' && pos + 1 < line.Length && line[pos + 1] == '>')
            {
                EmitPunctuation(line, ref pos, tokens, 2);
                return LexerState.Normal;
            }

            // Attribute name.
            if (IsIdentStart(line[pos]) || line[pos] == ':')
            {
                int attrStart = pos;
                while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '-' || line[pos] == ':'))
                    pos++;
                tokens.Add(new Token(attrStart, pos - attrStart, TokenType.TagAttribute));

                // =
                if (pos < line.Length && line[pos] == '=')
                {
                    EmitOperator(line, ref pos, tokens);

                    while (pos < line.Length && char.IsWhiteSpace(line[pos]))
                        pos++;

                    // Attribute value.
                    if (pos < line.Length && (line[pos] == '"' || line[pos] == '\''))
                    {
                        char quote = line[pos];
                        int valStart = pos;
                        pos++;
                        while (pos < line.Length && line[pos] != quote)
                            pos++;
                        if (pos < line.Length) pos++;
                        tokens.Add(new Token(valStart, pos - valStart, TokenType.TagAttributeValue));
                    }
                }
                continue;
            }

            tokens.Add(new Token(pos, 1, TokenType.Plain));
            pos++;
        }

        return new LexerState(StateInTag, 0);
    }

    private static LexerState ContinueXmlTag(string line, ref int pos, List<Token> tokens)
    {
        return ReadXmlAttributes(line, ref pos, tokens);
    }
}
