namespace Bascanka.Core.LineEnding;

/// <summary>
/// Enumerates the three standard line-ending conventions.
/// </summary>
public enum LineEndingType
{
    /// <summary>Carriage-return + line-feed (<c>\r\n</c>), standard on Windows.</summary>
    CRLF,

    /// <summary>Line-feed (<c>\n</c>), standard on Unix / macOS.</summary>
    LF,

    /// <summary>Carriage-return (<c>\r</c>), used by classic Mac OS (pre-OS X).</summary>
    CR,
}

/// <summary>
/// Manages the line-ending convention for a document.  Provides static helpers
/// to detect the dominant line-ending style in a text sample and to normalize
/// all line endings to a target style.
/// </summary>
public sealed class LineEndingManager
{
    /// <summary>
    /// Creates a new <see cref="LineEndingManager"/> with the given initial
    /// line-ending style.
    /// </summary>
    /// <param name="lineEnding">
    /// The line-ending type to use.  Defaults to
    /// <see cref="LineEndingType.LF"/> when not specified.
    /// </param>
    public LineEndingManager(LineEndingType lineEnding = LineEndingType.LF)
    {
        CurrentLineEnding = lineEnding;
    }

    /// <summary>
    /// The line-ending convention currently associated with the document.
    /// </summary>
    public LineEndingType CurrentLineEnding { get; set; }

    /// <summary>
    /// Returns the actual string representation of <see cref="CurrentLineEnding"/>.
    /// </summary>
    public string LineEndingString => CurrentLineEnding switch
    {
        LineEndingType.CRLF => "\r\n",
        LineEndingType.LF => "\n",
        LineEndingType.CR => "\r",
        _ => "\n",
    };

    /// <summary>
    /// Detects the dominant line-ending style in <paramref name="sampleText"/>
    /// by counting occurrences of each style and returning the majority.
    /// Returns <see cref="LineEndingType.LF"/> when no line endings are found
    /// (convention for new / single-line files).
    /// </summary>
    /// <param name="sampleText">A representative text sample to analyze.</param>
    /// <returns>The most frequently occurring line-ending type.</returns>
    public static LineEndingType Detect(string sampleText)
    {
        if (string.IsNullOrEmpty(sampleText))
            return LineEndingType.LF;

        int crlfCount = 0;
        int lfCount = 0;
        int crCount = 0;

        ReadOnlySpan<char> span = sampleText.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == '\r')
            {
                if (i + 1 < span.Length && span[i + 1] == '\n')
                {
                    crlfCount++;
                    i++; // Skip the '\n' of the CRLF pair.
                }
                else
                {
                    crCount++;
                }
            }
            else if (span[i] == '\n')
            {
                lfCount++;
            }
        }

        // If no line endings found, default to LF.
        if (crlfCount == 0 && lfCount == 0 && crCount == 0)
            return LineEndingType.LF;

        // Return the type with the highest count.
        if (crlfCount >= lfCount && crlfCount >= crCount)
            return LineEndingType.CRLF;

        if (lfCount >= crlfCount && lfCount >= crCount)
            return LineEndingType.LF;

        return LineEndingType.CR;
    }

    /// <summary>
    /// Normalizes all line endings in <paramref name="text"/> to the specified
    /// <paramref name="target"/> style.
    /// </summary>
    /// <param name="text">The input text.</param>
    /// <param name="target">The line-ending style to convert to.</param>
    /// <returns>A new string with uniform line endings.</returns>
    public static string Normalize(string text, LineEndingType target)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        string targetString = target switch
        {
            LineEndingType.CRLF => "\r\n",
            LineEndingType.LF => "\n",
            LineEndingType.CR => "\r",
            _ => "\n",
        };

        // First, replace all CRLF with LF to create a uniform base.
        // Then replace all remaining CR with LF.
        // Finally, replace all LF with the target.
        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

        if (target != LineEndingType.LF)
            normalized = normalized.Replace("\n", targetString);

        return normalized;
    }
}
