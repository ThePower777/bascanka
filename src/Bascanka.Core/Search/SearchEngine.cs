using System.Text.RegularExpressions;
using Bascanka.Core.Buffer;

namespace Bascanka.Core.Search;

/// <summary>
/// Provides find, replace, and count operations over a <see cref="PieceTable"/>
/// buffer.  Supports literal and regular-expression searches with options for
/// case sensitivity, whole-word matching, and directional (up/down) search with
/// optional wrap-around.
/// </summary>
public sealed class SearchEngine
{
    /// <summary>
    /// Maximum text length to extract from the buffer in a single pass.
    /// For very large documents the search is performed in overlapping windows.
    /// </summary>
    private const int WindowSize = 4 * 1024 * 1024; // 4 MB

    /// <summary>
    /// Maximum number of results returned by any FindAll method.
    /// Prevents OOM and UI freeze when a short pattern matches millions of times.
    /// </summary>
    public const int MaxResults = 100_000;

    /// <summary>
    /// Cached line boundaries for deduplicating <see cref="SearchResult.LineText"/>
    /// strings when multiple matches fall on the same line.
    /// </summary>
    [ThreadStatic]
    private static int _lastLineStart, _lastLineEnd;
    [ThreadStatic]
    private static string? _lastLineText;

    /// <summary>
    /// Finds the next match in <paramref name="buffer"/> starting from
    /// <paramref name="startOffset"/>, obeying the direction and wrap-around
    /// settings in <paramref name="options"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="SearchResult"/> for the first match found, or
    /// <see langword="null"/> if no match exists.
    /// </returns>
    public SearchResult? FindNext(PieceTable buffer, long startOffset, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(options);

        long docLength = buffer.Length;
        if (docLength == 0 || string.IsNullOrEmpty(options.Pattern))
            return null;

        startOffset = Math.Clamp(startOffset, 0, docLength);

        // Fast path: literal search without whole-word uses string.IndexOf.
        if (!options.UseRegex && !options.WholeWord)
        {
            var comparison = options.MatchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return FindNextLiteral(buffer, startOffset, docLength, options.Pattern, comparison, options.SearchUp, options.WrapAround);
        }

        Regex regex = BuildPattern(options);

        if (!options.SearchUp)
        {
            // Forward search: startOffset -> end, then optionally 0 -> startOffset.
            SearchResult? result = SearchRange(buffer, regex, startOffset, docLength - startOffset);
            if (result is not null)
                return result;

            if (options.WrapAround && startOffset > 0)
                return SearchRange(buffer, regex, 0, startOffset);
        }
        else
        {
            // Backward search: 0 -> startOffset (take last match), then optionally startOffset -> end.
            SearchResult? result = SearchRangeReverse(buffer, regex, 0, startOffset);
            if (result is not null)
                return result;

            if (options.WrapAround && startOffset < docLength)
                return SearchRangeReverse(buffer, regex, startOffset, docLength - startOffset);
        }

        return null;
    }

    /// <summary>
    /// Fast literal string search using <see cref="string.IndexOf(string, StringComparison)"/>
    /// instead of regex.
    /// </summary>
    private SearchResult? FindNextLiteral(PieceTable buffer, long startOffset, long docLength,
        string pattern, StringComparison comparison, bool searchUp, bool wrapAround)
    {
        int patLen = pattern.Length;

        if (!searchUp)
        {
            // Forward: search startOffset -> end in windows.
            var result = IndexOfInBuffer(buffer, startOffset, docLength - startOffset, pattern, comparison);
            if (result >= 0)
                return BuildResult(buffer, result, patLen);

            if (wrapAround && startOffset > 0)
            {
                result = IndexOfInBuffer(buffer, 0, Math.Min(startOffset + patLen, docLength), pattern, comparison);
                if (result >= 0)
                    return BuildResult(buffer, result, patLen);
            }
        }
        else
        {
            // Backward: search 0 -> startOffset, take last match.
            var result = LastIndexOfInBuffer(buffer, 0, startOffset, pattern, comparison);
            if (result >= 0)
                return BuildResult(buffer, result, patLen);

            if (wrapAround && startOffset < docLength)
            {
                result = LastIndexOfInBuffer(buffer, startOffset, docLength - startOffset, pattern, comparison);
                if (result >= 0)
                    return BuildResult(buffer, result, patLen);
            }
        }

        return null;
    }

    /// <summary>
    /// Searches for the first occurrence of <paramref name="pattern"/> in the buffer
    /// within the specified range using windowed <see cref="string.IndexOf(string, StringComparison)"/>.
    /// </summary>
    private static long IndexOfInBuffer(PieceTable buffer, long rangeStart, long rangeLength,
        string pattern, StringComparison comparison)
    {
        if (rangeLength < pattern.Length) return -1;

        int overlap = pattern.Length - 1;
        long offset = rangeStart;
        long rangeEnd = rangeStart + rangeLength;

        while (offset < rangeEnd)
        {
            long windowLen = Math.Min(WindowSize, rangeEnd - offset);
            string text = buffer.GetText(offset, windowLen);

            int idx = text.IndexOf(pattern, comparison);
            if (idx >= 0)
                return offset + idx;

            offset += windowLen - overlap;
        }

        return -1;
    }

    /// <summary>
    /// Searches for the last occurrence of <paramref name="pattern"/> in the buffer
    /// within the specified range using windowed <see cref="string.LastIndexOf(string, StringComparison)"/>.
    /// </summary>
    private static long LastIndexOfInBuffer(PieceTable buffer, long rangeStart, long rangeLength,
        string pattern, StringComparison comparison)
    {
        if (rangeLength < pattern.Length) return -1;

        int overlap = pattern.Length - 1;
        long rangeEnd = rangeStart + rangeLength;
        long bestMatch = -1;

        long offset = rangeStart;
        while (offset < rangeEnd)
        {
            long windowLen = Math.Min(WindowSize, rangeEnd - offset);
            string text = buffer.GetText(offset, windowLen);

            int idx = text.LastIndexOf(pattern, comparison);
            if (idx >= 0)
                bestMatch = offset + idx;

            offset += windowLen - overlap;
        }

        return bestMatch;
    }

    /// <summary>
    /// Finds all matches in <paramref name="buffer"/>.
    /// </summary>
    public List<SearchResult> FindAll(PieceTable buffer, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(options);

        var results = new List<SearchResult>();
        long docLength = buffer.Length;
        if (docLength == 0 || string.IsNullOrEmpty(options.Pattern))
            return results;

        // Fast literal path: string.IndexOf uses hardware vectorization,
        // ~10x faster than regex for plain text searches.
        if (!options.UseRegex && !options.WholeWord)
        {
            var comparison = options.MatchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            return FindAllLiteral(buffer, options.Pattern, comparison);
        }

        Regex regex = BuildPattern(options);

        // Process the entire document in overlapping windows so that a match
        // straddling a window boundary is not missed.
        int overlap = Math.Max(options.Pattern.Length * 4, 1024);

        long offset = 0;
        while (offset < docLength)
        {
            long windowLen = Math.Min(WindowSize, docLength - offset);
            string text = buffer.GetText(offset, windowLen);

            MatchCollection matches = regex.Matches(text);
            foreach (Match m in matches)
            {
                long absoluteOffset = offset + m.Index;

                // Skip duplicates that may appear in the overlap region.
                if (results.Count > 0 && absoluteOffset <= results[^1].Offset)
                    continue;

                results.Add(BuildResultFromWindow(buffer, text, offset, m.Index, m.Length));
                if (results.Count >= MaxResults) return results;
            }

            offset += windowLen - overlap;
            if (offset + overlap >= docLength)
                break;
        }

        // Catch any tail that the overlap logic may have skipped.
        if (offset > 0 && offset < docLength)
        {
            long tailLen = docLength - offset;
            string tailText = buffer.GetText(offset, tailLen);
            MatchCollection tailMatches = regex.Matches(tailText);

            foreach (Match m in tailMatches)
            {
                long absoluteOffset = offset + m.Index;
                if (results.Count > 0 && absoluteOffset <= results[^1].Offset)
                    continue;

                results.Add(BuildResultFromWindow(buffer, tailText, offset, m.Index, m.Length));
                if (results.Count >= MaxResults) return results;
            }
        }

        return results;
    }

    /// <summary>
    /// Literal fast path for <see cref="FindAll"/>: uses <see cref="string.IndexOf(string, StringComparison)"/>
    /// instead of regex, which is significantly faster for plain text searches.
    /// </summary>
    private List<SearchResult> FindAllLiteral(PieceTable buffer, string pattern, StringComparison comparison)
    {
        var results = new List<SearchResult>();
        long docLength = buffer.Length;
        int patLen = pattern.Length;
        int overlap = patLen - 1;

        long offset = 0;
        while (offset < docLength)
        {
            long windowLen = Math.Min(WindowSize, docLength - offset);
            string text = buffer.GetText(offset, windowLen);

            int searchStart = 0;
            while (true)
            {
                int idx = text.IndexOf(pattern, searchStart, comparison);
                if (idx < 0) break;

                long absoluteOffset = offset + idx;
                if (results.Count == 0 || absoluteOffset > results[^1].Offset)
                {
                    results.Add(BuildResultFromWindow(buffer, text, offset, idx, patLen));
                    if (results.Count >= MaxResults) return results;
                }

                searchStart = idx + 1;
            }

            offset += windowLen - overlap;
            if (offset + overlap >= docLength) break;
        }

        // Tail.
        if (offset > 0 && offset < docLength)
        {
            long tailLen = docLength - offset;
            string tailText = buffer.GetText(offset, tailLen);

            int searchStart = 0;
            while (true)
            {
                int idx = tailText.IndexOf(pattern, searchStart, comparison);
                if (idx < 0) break;

                long absoluteOffset = offset + idx;
                if (results.Count == 0 || absoluteOffset > results[^1].Offset)
                {
                    results.Add(BuildResultFromWindow(buffer, tailText, offset, idx, patLen));
                    if (results.Count >= MaxResults) return results;
                }

                searchStart = idx + 1;
            }
        }

        return results;
    }

    /// <summary>
    /// Asynchronously finds all matches in <paramref name="buffer"/>, running the
    /// search on a background thread. Supports cancellation and progress reporting.
    /// </summary>
    public async Task<List<SearchResult>> FindAllAsync(
        PieceTable buffer, SearchOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(options);

        long docLength = buffer.Length;
        if (docLength == 0 || string.IsNullOrEmpty(options.Pattern))
            return [];

        // Fast literal path.
        if (!options.UseRegex && !options.WholeWord)
        {
            var comparison = options.MatchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            return await FindAllLiteralAsync(buffer, options.Pattern, comparison, progress, cancellationToken);
        }

        // Build the regex on the calling thread (it's fast and validates the pattern).
        Regex regex = BuildPattern(options);
        int overlap = Math.Max(options.Pattern.Length * 4, 1024);

        // Inside Task.Run we use IsCancellationRequested (no throw) to exit
        // cooperatively.  Throwing inside Task.Run causes the debugger to
        // break on the first-chance OperationCanceledException even though
        // the caller catches it.  We throw once after await, on the calling
        // thread, where the catch block lives.
        var results = await Task.Run(() =>
        {
            if (cancellationToken.IsCancellationRequested) return new List<SearchResult>();

            var results = new List<SearchResult>();
            long offset = 0;

            while (offset < docLength)
            {
                if (cancellationToken.IsCancellationRequested) return results;

                long windowLen = Math.Min(WindowSize, docLength - offset);
                string text = buffer.GetText(offset, windowLen);

                MatchCollection matches = regex.Matches(text);
                foreach (Match m in matches)
                {
                    if (cancellationToken.IsCancellationRequested) return results;

                    long absoluteOffset = offset + m.Index;

                    // Skip duplicates that may appear in the overlap region.
                    if (results.Count > 0 && absoluteOffset <= results[^1].Offset)
                        continue;

                    results.Add(BuildResultFromWindow(buffer, text, offset, m.Index, m.Length));
                    if (results.Count >= MaxResults) { progress?.Report(100); return results; }
                }

                // Report progress as percentage of document scanned.
                progress?.Report((int)(offset * 100 / docLength));

                offset += windowLen - overlap;
                if (offset + overlap >= docLength)
                    break;
            }

            // Tail processing.
            if (offset > 0 && offset < docLength && !cancellationToken.IsCancellationRequested)
            {
                long tailLen = docLength - offset;
                string tailText = buffer.GetText(offset, tailLen);
                MatchCollection tailMatches = regex.Matches(tailText);

                foreach (Match m in tailMatches)
                {
                    if (cancellationToken.IsCancellationRequested) return results;

                    long absoluteOffset = offset + m.Index;
                    if (results.Count > 0 && absoluteOffset <= results[^1].Offset)
                        continue;

                    results.Add(BuildResultFromWindow(buffer, tailText, offset, m.Index, m.Length));
                    if (results.Count >= MaxResults) { progress?.Report(100); return results; }
                }
            }

            progress?.Report(100);
            return results;
        });

        if (cancellationToken.IsCancellationRequested)
            return new List<SearchResult>();
        return results;
    }

    /// <summary>
    /// Literal fast path for <see cref="FindAllAsync"/>: uses <see cref="string.IndexOf(string, StringComparison)"/>
    /// on a background thread with cancellation and progress support.
    /// </summary>
    private static async Task<List<SearchResult>> FindAllLiteralAsync(
        PieceTable buffer, string pattern, StringComparison comparison,
        IProgress<int>? progress, CancellationToken ct)
    {
        long docLength = buffer.Length;
        int patLen = pattern.Length;
        int overlap = patLen - 1;

        var results = await Task.Run(() =>
        {
            if (ct.IsCancellationRequested) return new List<SearchResult>();

            var results = new List<SearchResult>();
            long offset = 0;

            while (offset < docLength)
            {
                if (ct.IsCancellationRequested) return results;

                long windowLen = Math.Min(WindowSize, docLength - offset);
                string text = buffer.GetText(offset, windowLen);

                int searchStart = 0;
                while (true)
                {
                    int idx = text.IndexOf(pattern, searchStart, comparison);
                    if (idx < 0) break;

                    long absoluteOffset = offset + idx;
                    if (results.Count == 0 || absoluteOffset > results[^1].Offset)
                    {
                        results.Add(BuildResultFromWindow(buffer, text, offset, idx, patLen));
                        if (results.Count >= MaxResults) { progress?.Report(100); return results; }
                    }

                    searchStart = idx + 1;
                }

                progress?.Report((int)(offset * 100 / docLength));

                offset += windowLen - overlap;
                if (offset + overlap >= docLength) break;
            }

            // Tail.
            if (offset > 0 && offset < docLength && !ct.IsCancellationRequested)
            {
                long tailLen = docLength - offset;
                string tailText = buffer.GetText(offset, tailLen);

                int searchStart = 0;
                while (true)
                {
                    int idx = tailText.IndexOf(pattern, searchStart, comparison);
                    if (idx < 0) break;

                    long absoluteOffset = offset + idx;
                    if (results.Count == 0 || absoluteOffset > results[^1].Offset)
                    {
                        results.Add(BuildResultFromWindow(buffer, tailText, offset, idx, patLen));
                        if (results.Count >= MaxResults) { progress?.Report(100); return results; }
                    }

                    searchStart = idx + 1;
                }
            }

            progress?.Report(100);
            return results;
        });

        if (ct.IsCancellationRequested)
            return new List<SearchResult>();
        return results;
    }

    /// <summary>
    /// Replaces the text matched by <paramref name="match"/> with
    /// <paramref name="replacement"/>.  For regex searches, back-references
    /// in the replacement string are expanded.
    /// </summary>
    public void Replace(PieceTable buffer, SearchResult match, string replacement, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(options);

        string actual = buffer.GetText(match.Offset, match.Length);

        string expandedReplacement;
        if (options.UseRegex)
        {
            Regex regex = BuildPattern(options);
            expandedReplacement = regex.Replace(actual, replacement);
        }
        else
        {
            expandedReplacement = replacement;
        }

        buffer.Delete(match.Offset, match.Length);
        buffer.Insert(match.Offset, expandedReplacement);
    }

    /// <summary>
    /// Replaces every occurrence of the pattern in <paramref name="buffer"/>
    /// with <paramref name="replacement"/>.
    /// </summary>
    /// <returns>The number of replacements made.</returns>
    public int ReplaceAll(PieceTable buffer, string replacement, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(options);

        List<SearchResult> matches = FindAll(buffer, options);
        if (matches.Count == 0)
            return 0;

        Regex? regex = options.UseRegex ? BuildPattern(options) : null;

        // Replace from the end of the document backwards so that offsets of
        // earlier matches remain valid.
        int count = 0;
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            SearchResult m = matches[i];
            string actual = buffer.GetText(m.Offset, m.Length);

            string expanded = regex is not null
                ? regex.Replace(actual, replacement)
                : replacement;

            buffer.Delete(m.Offset, m.Length);
            buffer.Insert(m.Offset, expanded);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Counts the number of matches in <paramref name="buffer"/> without
    /// constructing full result objects.
    /// </summary>
    public int CountMatches(PieceTable buffer, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(options);

        long docLength = buffer.Length;
        if (docLength == 0 || string.IsNullOrEmpty(options.Pattern))
            return 0;

        // Fast literal path.
        if (!options.UseRegex && !options.WholeWord)
        {
            var comparison = options.MatchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            return CountMatchesLiteral(buffer, options.Pattern, comparison);
        }

        Regex regex = BuildPattern(options);

        int count = 0;
        int overlap = Math.Max(options.Pattern.Length * 4, 1024);
        long prevLastOffset = -1;

        long offset = 0;
        while (offset < docLength)
        {
            long windowLen = Math.Min(WindowSize, docLength - offset);
            string text = buffer.GetText(offset, windowLen);

            foreach (Match m in regex.Matches(text))
            {
                long abs = offset + m.Index;
                if (abs <= prevLastOffset)
                    continue;

                count++;
                prevLastOffset = abs;
            }

            offset += windowLen - overlap;
            if (offset + overlap >= docLength)
                break;
        }

        // Tail — only if offset is still a valid positive position.
        if (offset > 0 && offset < docLength)
        {
            long tailLen = docLength - offset;
            string tailText = buffer.GetText(offset, tailLen);
            foreach (Match m in regex.Matches(tailText))
            {
                long abs = offset + m.Index;
                if (abs <= prevLastOffset)
                    continue;
                count++;
                prevLastOffset = abs;
            }
        }

        return count;
    }

    /// <summary>
    /// Literal fast path for <see cref="CountMatches"/>: uses
    /// <see cref="string.IndexOf(string, StringComparison)"/> instead of regex.
    /// </summary>
    private static int CountMatchesLiteral(PieceTable buffer, string pattern, StringComparison comparison)
    {
        long docLength = buffer.Length;
        int patLen = pattern.Length;
        int overlap = patLen - 1;
        int count = 0;
        long prevLastOffset = -1;

        long offset = 0;
        while (offset < docLength)
        {
            long windowLen = Math.Min(WindowSize, docLength - offset);
            string text = buffer.GetText(offset, windowLen);

            int searchStart = 0;
            while (true)
            {
                int idx = text.IndexOf(pattern, searchStart, comparison);
                if (idx < 0) break;

                long abs = offset + idx;
                if (abs > prevLastOffset)
                {
                    count++;
                    prevLastOffset = abs;
                }

                searchStart = idx + 1;
            }

            offset += windowLen - overlap;
            if (offset + overlap >= docLength) break;
        }

        // Tail.
        if (offset > 0 && offset < docLength)
        {
            long tailLen = docLength - offset;
            string tailText = buffer.GetText(offset, tailLen);

            int searchStart = 0;
            while (true)
            {
                int idx = tailText.IndexOf(pattern, searchStart, comparison);
                if (idx < 0) break;

                long abs = offset + idx;
                if (abs > prevLastOffset)
                {
                    count++;
                    prevLastOffset = abs;
                }

                searchStart = idx + 1;
            }
        }

        return count;
    }

    /// <summary>
    /// Converts <see cref="SearchOptions"/> into a compiled <see cref="Regex"/>
    /// that respects literal vs. regex mode, case sensitivity, and whole-word matching.
    /// </summary>
    public static Regex BuildPattern(SearchOptions options)
    {
        string pattern = options.UseRegex
            ? options.Pattern
            : Regex.Escape(options.Pattern);

        if (options.WholeWord)
            pattern = $@"\b{pattern}\b";

        RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.Multiline;
        if (!options.MatchCase)
            regexOptions |= RegexOptions.IgnoreCase;

        return new Regex(pattern, regexOptions, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Searches a sub-range of the buffer for the first forward match,
    /// using windowed 4 MB passes to avoid loading the entire range as
    /// a single string (which would OOM on large files).
    /// </summary>
    private SearchResult? SearchRange(PieceTable buffer, Regex regex, long rangeStart, long rangeLength)
    {
        if (rangeLength <= 0) return null;

        // Small range: single pass (no windowing overhead).
        if (rangeLength <= WindowSize)
        {
            string text = buffer.GetText(rangeStart, rangeLength);
            Match m = regex.Match(text);
            if (!m.Success) return null;
            return BuildResult(buffer, rangeStart + m.Index, m.Length);
        }

        // Large range: windowed search.
        int overlap = Math.Max(4096, 1024);
        long rangeEnd = rangeStart + rangeLength;
        long offset = rangeStart;

        while (offset < rangeEnd)
        {
            long windowLen = Math.Min(WindowSize, rangeEnd - offset);
            string text = buffer.GetText(offset, windowLen);

            Match m = regex.Match(text);
            if (m.Success)
                return BuildResult(buffer, offset + m.Index, m.Length);

            if (windowLen < WindowSize)
                break;

            offset += windowLen - overlap;
        }

        return null;
    }

    /// <summary>
    /// Searches a sub-range of the buffer and returns the last match (for
    /// backward / search-up mode), using windowed 4 MB passes.
    /// </summary>
    private SearchResult? SearchRangeReverse(PieceTable buffer, Regex regex, long rangeStart, long rangeLength)
    {
        if (rangeLength <= 0) return null;

        // Small range: single pass.
        if (rangeLength <= WindowSize)
        {
            string text = buffer.GetText(rangeStart, rangeLength);
            MatchCollection matches = regex.Matches(text);
            if (matches.Count == 0) return null;
            Match last = matches[^1];
            return BuildResult(buffer, rangeStart + last.Index, last.Length);
        }

        // Large range: windowed search — keep track of the last match found.
        int overlap = Math.Max(4096, 1024);
        long rangeEnd = rangeStart + rangeLength;
        SearchResult? lastResult = null;
        long offset = rangeStart;

        while (offset < rangeEnd)
        {
            long windowLen = Math.Min(WindowSize, rangeEnd - offset);
            string text = buffer.GetText(offset, windowLen);

            MatchCollection matches = regex.Matches(text);
            if (matches.Count > 0)
            {
                Match last = matches[^1];
                long absOffset = offset + last.Index;
                if (absOffset + last.Length <= rangeEnd)
                    lastResult = BuildResult(buffer, absOffset, last.Length);
            }

            if (windowLen < WindowSize)
                break;

            offset += windowLen - overlap;
        }

        return lastResult;
    }

    /// <summary>
    /// Constructs a <see cref="SearchResult"/> by computing line/column information
    /// from the absolute offset in the buffer using the PieceTable's O(log N)
    /// line-offset cache.  Used for single-result paths (FindNext/FindPrevious).
    /// </summary>
    private static SearchResult BuildResult(PieceTable buffer, long absoluteOffset, int matchLength)
    {
        var (line, column) = buffer.OffsetToLineColumn(absoluteOffset);

        string lineText = line < buffer.LineCount
            ? buffer.GetLine(line)
            : string.Empty;

        return new SearchResult
        {
            Offset = absoluteOffset,
            Length = matchLength,
            LineNumber = line + 1,      // 1-based
            ColumnNumber = (int)column + 1, // 1-based
            LineText = lineText,
        };
    }

    /// <summary>
    /// Constructs a <see cref="SearchResult"/> by extracting line text directly
    /// from the already-loaded window text, avoiding an expensive per-match
    /// <see cref="PieceTable.GetLine"/> tree walk + chunk-cache access.
    /// Used for bulk search paths (FindAll/FindAllAsync) where there may be
    /// thousands of matches.
    /// </summary>
    private static SearchResult BuildResultFromWindow(
        PieceTable buffer, string windowText, long windowOffset, int matchIndex, int matchLength)
    {
        long absoluteOffset = windowOffset + matchIndex;

        // OffsetToLineColumn is O(log lineCount) via binary search on the
        // pre-built line-offset cache — fast even for very large files.
        var (line, column) = buffer.OffsetToLineColumn(absoluteOffset);

        // Extract the line text from the window instead of calling
        // buffer.GetLine(line) which would do an O(log N) tree walk per match.
        int lineStart;
        if (matchIndex == 0)
        {
            lineStart = 0;
        }
        else
        {
            int prevNewline = windowText.LastIndexOf('\n', matchIndex - 1);
            lineStart = prevNewline < 0 ? 0 : prevNewline + 1;
        }

        int lineEnd = windowText.IndexOf('\n', matchIndex);
        if (lineEnd < 0) lineEnd = windowText.Length;

        // Reuse the string when consecutive matches fall on the same line
        // to avoid allocating duplicate copies (critical for dense matches).
        string lineText;
        if (lineStart == _lastLineStart && lineEnd == _lastLineEnd && _lastLineText is not null)
        {
            lineText = _lastLineText;
        }
        else
        {
            lineText = windowText.Substring(lineStart, lineEnd - lineStart);
            _lastLineStart = lineStart;
            _lastLineEnd = lineEnd;
            _lastLineText = lineText;
        }

        return new SearchResult
        {
            Offset = absoluteOffset,
            Length = matchLength,
            LineNumber = line + 1,      // 1-based
            ColumnNumber = (int)column + 1, // 1-based
            LineText = lineText,
        };
    }
}
