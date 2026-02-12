namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for Markdown.  Handles headings (<c>#</c>), bold (<c>**</c>),
/// italic (<c>*</c>), inline code (<c>`</c>), fenced code blocks
/// (<c>```</c>), links (<c>[text](url)</c>), and list markers.
/// </summary>
public sealed class MarkdownLexer : BaseLexer
{
    private const int StateInFencedCode = 10;

    public override string LanguageId => "markdown";
    public override string[] FileExtensions => [".md", ".markdown", ".mdown", ".mkd", ".mkdn"];

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        // Fenced code block start: ``` or ~~~
        if (pos == 0 && (StartsWith(line, 0, "```") || StartsWith(line, 0, "~~~")))
        {
            tokens.Add(new Token(0, line.Length, TokenType.MarkdownCode));
            pos = line.Length;
            return new LexerState(StateInFencedCode, 0);
        }

        // Heading: # at start of line.
        if (pos == 0 && line.Length > 0 && line[0] == '#')
        {
            tokens.Add(new Token(0, line.Length, TokenType.MarkdownHeading));
            pos = line.Length;
            return state;
        }

        // We process inline elements character by character.
        char c = line[pos];

        // Bold: **...**  or __...__
        if ((c == '*' && pos + 1 < line.Length && line[pos + 1] == '*') ||
            (c == '_' && pos + 1 < line.Length && line[pos + 1] == '_'))
        {
            char marker = c;
            string closeMarker = new(marker, 2);
            int start = pos;
            pos += 2;
            int closeIdx = line.IndexOf(closeMarker, pos, StringComparison.Ordinal);
            if (closeIdx >= 0)
            {
                pos = closeIdx + 2;
                tokens.Add(new Token(start, pos - start, TokenType.MarkdownBold));
                return state;
            }
            // No close found: emit as plain.
            tokens.Add(new Token(start, 2, TokenType.Plain));
            return state;
        }

        // Italic: *...* or _..._
        if ((c == '*' || c == '_') &&
            pos + 1 < line.Length && line[pos + 1] != c && !char.IsWhiteSpace(line[pos + 1]))
        {
            char marker = c;
            int start = pos;
            pos++;
            int closeIdx = line.IndexOf(marker, pos);
            if (closeIdx > pos)
            {
                pos = closeIdx + 1;
                tokens.Add(new Token(start, pos - start, TokenType.MarkdownItalic));
                return state;
            }
            // No close found: emit as plain.
            tokens.Add(new Token(start, 1, TokenType.Plain));
            return state;
        }

        // Inline code: `...`
        if (c == '`')
        {
            int start = pos;
            int backtickCount = 0;
            while (pos < line.Length && line[pos] == '`')
            {
                backtickCount++;
                pos++;
            }

            string closePattern = new('`', backtickCount);
            int closeIdx = line.IndexOf(closePattern, pos, StringComparison.Ordinal);
            if (closeIdx >= 0)
            {
                pos = closeIdx + backtickCount;
                tokens.Add(new Token(start, pos - start, TokenType.MarkdownCode));
                return state;
            }

            // No closing backticks found.
            tokens.Add(new Token(start, pos - start, TokenType.MarkdownCode));
            return state;
        }

        // Link: [text](url)
        if (c == '[')
        {
            int start = pos;
            int closeBracket = line.IndexOf(']', pos + 1);
            if (closeBracket > pos &&
                closeBracket + 1 < line.Length && line[closeBracket + 1] == '(')
            {
                int closeParen = line.IndexOf(')', closeBracket + 2);
                if (closeParen > closeBracket + 1)
                {
                    pos = closeParen + 1;
                    tokens.Add(new Token(start, pos - start, TokenType.MarkdownLink));
                    return state;
                }
            }
            // Not a valid link -- emit [ as plain.
            tokens.Add(new Token(pos, 1, TokenType.Plain));
            pos++;
            return state;
        }

        // Image: ![alt](url)
        if (c == '!' && pos + 1 < line.Length && line[pos + 1] == '[')
        {
            int start = pos;
            int closeBracket = line.IndexOf(']', pos + 2);
            if (closeBracket > pos &&
                closeBracket + 1 < line.Length && line[closeBracket + 1] == '(')
            {
                int closeParen = line.IndexOf(')', closeBracket + 2);
                if (closeParen > closeBracket + 1)
                {
                    pos = closeParen + 1;
                    tokens.Add(new Token(start, pos - start, TokenType.MarkdownLink));
                    return state;
                }
            }
        }

        // Horizontal rule: --- or *** or ___ (three or more at start of line).
        if (pos == 0 && line.Length >= 3)
        {
            char first = line[0];
            if ((first == '-' || first == '*' || first == '_') &&
                line.All(ch => ch == first || ch == ' '))
            {
                int count = line.Count(ch => ch == first);
                if (count >= 3)
                {
                    tokens.Add(new Token(0, line.Length, TokenType.Punctuation));
                    pos = line.Length;
                    return state;
                }
            }
        }

        // List marker: -, *, +, or digit followed by . at start of line.
        if (pos == 0)
        {
            string trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;

            if (trimmed.Length > 0 && (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+'))
            {
                if (trimmed.Length > 1 && trimmed[1] == ' ')
                {
                    int markerLen = indent + 2;
                    tokens.Add(new Token(0, markerLen, TokenType.Punctuation));
                    pos = markerLen;
                    return state;
                }
            }

            // Ordered list: 1. , 2. , etc.
            if (trimmed.Length > 0 && char.IsDigit(trimmed[0]))
            {
                int i = 0;
                while (i < trimmed.Length && char.IsDigit(trimmed[i]))
                    i++;
                if (i < trimmed.Length && trimmed[i] == '.' && i + 1 < trimmed.Length && trimmed[i + 1] == ' ')
                {
                    int markerLen = indent + i + 2;
                    tokens.Add(new Token(0, markerLen, TokenType.Punctuation));
                    pos = markerLen;
                    return state;
                }
            }
        }

        // Blockquote: > at start of line.
        if (pos == 0 && line.TrimStart().StartsWith('>'))
        {
            int start = 0;
            int end = 0;
            while (end < line.Length && (line[end] == '>' || line[end] == ' '))
                end++;
            tokens.Add(new Token(start, end, TokenType.Punctuation));
            pos = end;
            return state;
        }

        // Plain text: consume until we hit a special character.
        int textStart = pos;
        while (pos < line.Length)
        {
            char ch = line[pos];
            if (ch == '*' || ch == '_' || ch == '`' || ch == '[' || ch == '!' || ch == '#')
                break;
            pos++;
        }
        if (pos > textStart)
        {
            tokens.Add(new Token(textStart, pos - textStart, TokenType.Plain));
        }

        return state;
    }

    protected override LexerState ContinueMultiLineState(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (state.StateId == StateInFencedCode)
        {
            // Check for closing fence.
            if (StartsWith(line, 0, "```") || StartsWith(line, 0, "~~~"))
            {
                tokens.Add(new Token(0, line.Length, TokenType.MarkdownCode));
                pos = line.Length;
                return LexerState.Normal;
            }

            tokens.Add(new Token(0, line.Length, TokenType.MarkdownCode));
            pos = line.Length;
            return state;
        }

        return state;
    }
}
