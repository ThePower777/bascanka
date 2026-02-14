using System.Drawing;
using System.Text.RegularExpressions;

namespace Bascanka.Editor.Highlighting;

/// <summary>
/// A colored span produced by a match-scope rule.
/// </summary>
public readonly struct CustomColorSpan
{
    public int Start { get; init; }
    public int Length { get; init; }
    public Color Foreground { get; init; }
    public Color Background { get; init; }
}

/// <summary>
/// Result of matching a single line against all rules.
/// </summary>
public readonly struct CustomLineResult
{
    /// <summary>Background from first matching line rule, or <see cref="Color.Empty"/>.</summary>
    public Color LineBackground { get; init; }

    /// <summary>Default foreground from first matching line rule, or <see cref="Color.Empty"/>.</summary>
    public Color LineForeground { get; init; }

    /// <summary>All match-rule hits (may be null if none).</summary>
    public List<CustomColorSpan>? Spans { get; init; }
}

/// <summary>
/// A multiline block region produced by a block-scope rule.
/// </summary>
public readonly struct BlockRegion
{
    public long StartLine { get; init; }
    public long EndLine { get; init; }
    public Color Foreground { get; init; }
    public Color Background { get; init; }
    public bool Foldable { get; init; }
}

/// <summary>
/// Pre-compiles regexes from a <see cref="CustomHighlightProfile"/> and
/// provides fast per-line matching.
/// </summary>
public sealed class CustomHighlightMatcher
{
    private readonly (Regex Regex, Color Foreground, Color Background)[] _lineRules;
    private readonly (Regex Regex, Color Foreground, Color Background)[] _matchRules;
    private readonly (Regex Begin, Regex End, Color Foreground, Color Background, bool Foldable)[] _blockRules;

    /// <summary>Whether this matcher has any block-scope rules.</summary>
    public bool HasBlockRules => _blockRules.Length > 0;

    public CustomHighlightMatcher(CustomHighlightProfile profile)
    {
        var lineRules = new List<(Regex, Color, Color)>();
        var matchRules = new List<(Regex, Color, Color)>();
        var blockRules = new List<(Regex, Regex, Color, Color, bool)>();

        foreach (var rule in profile.Rules)
        {
            if (string.Equals(rule.Scope, "block", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(rule.BeginPattern) || string.IsNullOrEmpty(rule.EndPattern))
                    continue;

                Regex beginRx, endRx;
                try
                {
                    beginRx = new Regex(rule.BeginPattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
                    endRx = new Regex(rule.EndPattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
                }
                catch (ArgumentException)
                {
                    continue;
                }

                blockRules.Add((beginRx, endRx, rule.Foreground, rule.Background, rule.Foldable));
                continue;
            }

            if (string.IsNullOrEmpty(rule.Pattern)) continue;

            Regex regex;
            try
            {
                regex = new Regex(rule.Pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
            }
            catch (ArgumentException)
            {
                continue; // skip invalid patterns
            }

            var entry = (regex, rule.Foreground, rule.Background);

            if (string.Equals(rule.Scope, "line", StringComparison.OrdinalIgnoreCase))
                lineRules.Add(entry);
            else
                matchRules.Add(entry);
        }

        _lineRules = lineRules.ToArray();
        _matchRules = matchRules.ToArray();
        _blockRules = blockRules.ToArray();
    }

    /// <summary>
    /// Scans all lines to find block regions for all block rules.
    /// </summary>
    /// <param name="getLineRange">Batch line fetcher: (startLine, count) → array of line texts.</param>
    /// <param name="lineCount">Total number of lines in the document.</param>
    public List<BlockRegion> ScanBlocks(Func<long, int, string[]> getLineRange, long lineCount)
    {
        var regions = new List<BlockRegion>();
        if (_blockRules.Length == 0 || lineCount == 0) return regions;

        // Per-rule state: the line where the current open block started (-1 = not in block).
        var openStartLine = new long[_blockRules.Length];
        for (int r = 0; r < _blockRules.Length; r++)
            openStartLine[r] = -1;

        const int batchSize = 1000;
        for (long batchStart = 0; batchStart < lineCount; batchStart += batchSize)
        {
            int count = (int)Math.Min(batchSize, lineCount - batchStart);
            string[] lines = getLineRange(batchStart, count);

            for (int i = 0; i < lines.Length; i++)
            {
                long line = batchStart + i;
                string text = lines[i].TrimEnd('\r');

                for (int r = 0; r < _blockRules.Length; r++)
                {
                    try
                    {
                        if (openStartLine[r] < 0)
                        {
                            // Not in block — test begin pattern.
                            if (_blockRules[r].Begin.IsMatch(text))
                                openStartLine[r] = line;
                        }
                        else
                        {
                            // In block — test end pattern.
                            if (_blockRules[r].End.IsMatch(text))
                            {
                                regions.Add(new BlockRegion
                                {
                                    StartLine = openStartLine[r],
                                    EndLine = line,
                                    Foreground = _blockRules[r].Foreground,
                                    Background = _blockRules[r].Background,
                                    Foldable = _blockRules[r].Foldable,
                                });
                                openStartLine[r] = -1;
                            }
                        }
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        // Skip slow patterns.
                    }
                }
            }
        }

        // Unterminated blocks extend to end of document.
        for (int r = 0; r < _blockRules.Length; r++)
        {
            if (openStartLine[r] >= 0)
            {
                regions.Add(new BlockRegion
                {
                    StartLine = openStartLine[r],
                    EndLine = lineCount - 1,
                    Foreground = _blockRules[r].Foreground,
                    Background = _blockRules[r].Background,
                    Foldable = _blockRules[r].Foldable,
                });
            }
        }

        regions.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));
        return regions;
    }

    /// <summary>
    /// Binary search for the first block region containing the given line.
    /// </summary>
    public static BlockRegion? GetBlockForLine(IReadOnlyList<BlockRegion> regions, long line)
    {
        if (regions is null || regions.Count == 0) return null;

        int lo = 0, hi = regions.Count - 1;
        BlockRegion? result = null;

        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            var r = regions[mid];

            if (line < r.StartLine)
            {
                hi = mid - 1;
            }
            else if (line > r.EndLine)
            {
                lo = mid + 1;
            }
            else
            {
                // line is within this region.
                result = r;
                // There may be earlier regions also containing this line; search left.
                hi = mid - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates all rules against the given line text.
    /// </summary>
    public CustomLineResult MatchLine(string lineText)
    {
        Color lineBg = Color.Empty;
        Color lineFg = Color.Empty;

        // Line rules: first match wins.
        for (int i = 0; i < _lineRules.Length; i++)
        {
            try
            {
                if (_lineRules[i].Regex.IsMatch(lineText))
                {
                    lineBg = _lineRules[i].Background;
                    lineFg = _lineRules[i].Foreground;
                    break;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // skip slow patterns
            }
        }

        // Match rules: find all occurrences.
        List<CustomColorSpan>? spans = null;
        for (int i = 0; i < _matchRules.Length; i++)
        {
            try
            {
                var matches = _matchRules[i].Regex.Matches(lineText);
                foreach (Match m in matches)
                {
                    if (m.Length == 0) continue;
                    spans ??= new List<CustomColorSpan>();
                    spans.Add(new CustomColorSpan
                    {
                        Start = m.Index,
                        Length = m.Length,
                        Foreground = _matchRules[i].Foreground,
                        Background = _matchRules[i].Background,
                    });
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // skip slow patterns
            }
        }

        // Sort spans by start position for rendering.
        if (spans is { Count: > 1 })
            spans.Sort((a, b) => a.Start.CompareTo(b.Start));

        return new CustomLineResult
        {
            LineBackground = lineBg,
            LineForeground = lineFg,
            Spans = spans,
        };
    }
}
