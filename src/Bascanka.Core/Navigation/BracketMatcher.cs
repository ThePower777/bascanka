using Bascanka.Core.Buffer;

namespace Bascanka.Core.Navigation;

/// <summary>
/// Provides static bracket-matching functionality over a <see cref="PieceTable"/>
/// buffer.  Handles <c>()</c>, <c>[]</c>, <c>{}</c>, and <c>&lt;&gt;</c>,
/// and applies a basic heuristic to skip brackets that appear inside string
/// literals or single-line comments.
/// </summary>
public static class BracketMatcher
{
    /// <summary>Maps opening brackets to their closing counterparts.</summary>
    private static readonly Dictionary<char, char> OpenToClose = new()
    {
        ['('] = ')',
        ['['] = ']',
        ['{'] = '}',
        ['<'] = '>',
    };

    /// <summary>Maps closing brackets to their opening counterparts.</summary>
    private static readonly Dictionary<char, char> CloseToOpen = new()
    {
        [')'] = '(',
        [']'] = '[',
        ['}'] = '{',
        ['>'] = '<',
    };

    /// <summary>
    /// Attempts to find the matching bracket for the character at
    /// <paramref name="offset"/> in the given <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The text buffer to search.</param>
    /// <param name="offset">
    /// Zero-based character offset of a bracket character.
    /// </param>
    /// <returns>
    /// A tuple of (openOffset, closeOffset) if a matching pair is found;
    /// <see langword="null"/> if the character at <paramref name="offset"/>
    /// is not a recognized bracket or no match can be found.
    /// </returns>
    public static (long openOffset, long closeOffset)? FindMatchingBracket(
        PieceTable buffer, long offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        long length = buffer.Length;
        if (offset < 0 || offset >= length)
            return null;

        char ch = buffer.GetText(offset, 1)[0];

        if (OpenToClose.TryGetValue(ch, out char expectedClose))
        {
            // The character is an opening bracket -- scan forward for its match.
            long? closePos = ScanForward(buffer, offset, ch, expectedClose);
            return closePos.HasValue ? (offset, closePos.Value) : null;
        }

        if (CloseToOpen.TryGetValue(ch, out char expectedOpen))
        {
            // The character is a closing bracket -- scan backward for its match.
            long? openPos = ScanBackward(buffer, offset, ch, expectedOpen);
            return openPos.HasValue ? (openPos.Value, offset) : null;
        }

        return null;
    }

    /// <summary>
    /// Scans forward from <paramref name="startOffset"/> + 1 for the matching
    /// <paramref name="closeBracket"/>, respecting nesting and skipping
    /// brackets inside strings and comments.
    /// </summary>
    private static long? ScanForward(
        PieceTable buffer, long startOffset, char openBracket, char closeBracket)
    {
        long length = buffer.Length;
        int depth = 1;
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inLineComment = false;

        for (long i = startOffset + 1; i < length; i++)
        {
            char c = GetChar(buffer, i);

            // Newline resets line-comment state.
            if (c == '\n')
            {
                inLineComment = false;
                continue;
            }

            if (inLineComment)
                continue;

            // Detect line comment start: //
            if (!inSingleQuote && !inDoubleQuote && c == '/' && i + 1 < length)
            {
                char next = GetChar(buffer, i + 1);
                if (next == '/')
                {
                    inLineComment = true;
                    i++; // skip second '/'
                    continue;
                }
            }

            // Toggle string states (with basic escape handling).
            if (!inLineComment)
            {
                if (c == '\'' && !inDoubleQuote)
                {
                    // Check for escape: if the previous char is '\' then skip.
                    if (i > 0 && GetChar(buffer, i - 1) == '\\')
                        continue;
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (c == '"' && !inSingleQuote)
                {
                    if (i > 0 && GetChar(buffer, i - 1) == '\\')
                        continue;
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }
            }

            if (inSingleQuote || inDoubleQuote)
                continue;

            if (c == openBracket) depth++;
            else if (c == closeBracket)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Scans backward from <paramref name="startOffset"/> - 1 for the matching
    /// <paramref name="openBracket"/>, respecting nesting and skipping
    /// brackets inside strings (comment detection is limited in reverse).
    /// </summary>
    private static long? ScanBackward(
        PieceTable buffer, long startOffset, char closeBracket, char openBracket)
    {
        int depth = 1;
        bool inSingleQuote = false;
        bool inDoubleQuote = false;

        for (long i = startOffset - 1; i >= 0; i--)
        {
            char c = GetChar(buffer, i);

            // Simple string toggle (reverse scan is inherently less precise).
            if (c == '\'' && !inDoubleQuote)
            {
                if (i > 0 && GetChar(buffer, i - 1) == '\\')
                    continue;
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                if (i > 0 && GetChar(buffer, i - 1) == '\\')
                    continue;
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (inSingleQuote || inDoubleQuote)
                continue;

            // Skip characters inside a line comment.  Heuristic: if we see
            // "//" earlier on the same line, the bracket was inside a comment.
            if (IsInsideLineComment(buffer, i))
                continue;

            if (c == closeBracket) depth++;
            else if (c == openBracket)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Basic heuristic: checks whether <paramref name="offset"/> falls after
    /// a <c>//</c> sequence on the same line (indicating a line comment).
    /// </summary>
    private static bool IsInsideLineComment(PieceTable buffer, long offset)
    {
        // Walk backward to the start of the line.
        long lineStart = offset;
        while (lineStart > 0)
        {
            char c = GetChar(buffer, lineStart - 1);
            if (c == '\n')
                break;
            lineStart--;
        }

        // Scan forward from line start looking for "//".
        bool inSingle = false;
        bool inDouble = false;
        for (long i = lineStart; i < offset; i++)
        {
            char c = GetChar(buffer, i);

            if (c == '\'' && !inDouble)
            {
                if (i > 0 && GetChar(buffer, i - 1) == '\\') continue;
                inSingle = !inSingle;
                continue;
            }
            if (c == '"' && !inSingle)
            {
                if (i > 0 && GetChar(buffer, i - 1) == '\\') continue;
                inDouble = !inDouble;
                continue;
            }

            if (!inSingle && !inDouble && c == '/' && i + 1 <= offset)
            {
                char next = GetChar(buffer, i + 1);
                if (next == '/')
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Retrieves a single character from the buffer at the given offset.
    /// </summary>
    private static char GetChar(PieceTable buffer, long offset)
    {
        return buffer.GetCharAt(offset);
    }
}
