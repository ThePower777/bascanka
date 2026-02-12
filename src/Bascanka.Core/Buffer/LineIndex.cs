namespace Bascanka.Core.Buffer;

/// <summary>
/// A sparse index that records the character offset of every Nth line in a
/// text source.  Designed for large files where scanning from the beginning
/// on every line look-up would be too expensive.
/// <para>
/// The index is built asynchronously via <see cref="BuildAsync"/> and can
/// report progress through an <see cref="IProgress{T}"/> callback.
/// </para>
/// </summary>
public sealed class LineIndex
{
    /// <summary>
    /// Default sampling interval: one entry per 1 000 lines.
    /// </summary>
    public const int DefaultSampleInterval = 1000;

    private readonly int _sampleInterval;

    // _entries[k] = character offset of line (k * _sampleInterval).
    // Line 0 is always at offset 0, so _entries[0] == 0.
    private long[] _entries = Array.Empty<long>();

    // Total number of lines discovered during the last build.
    private long _totalLines;

    // The text source that was used to build this index.
    // Kept so that forward-scan from the nearest checkpoint can read chars.
    private ITextSource? _source;

    /// <summary>
    /// Creates a new <see cref="LineIndex"/> with the given sampling interval.
    /// </summary>
    /// <param name="sampleInterval">
    /// Record one entry for every <paramref name="sampleInterval"/> lines.
    /// Must be at least 1.
    /// </param>
    public LineIndex(int sampleInterval = DefaultSampleInterval)
    {
        if (sampleInterval < 1)
            throw new ArgumentOutOfRangeException(nameof(sampleInterval));

        _sampleInterval = sampleInterval;
    }

    /// <summary>
    /// The sampling interval used by this index.
    /// </summary>
    public int SampleInterval => _sampleInterval;

    /// <summary>
    /// Total number of lines discovered during the most recent build.
    /// A document with no line-feed characters has 1 line.
    /// </summary>
    public long TotalLines => _totalLines;

    /// <summary>
    /// Whether the index has been built at least once.
    /// </summary>
    public bool IsBuilt => _source is not null;

    // ────────────────────────────────────────────────────────────────────
    //  Build
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds (or rebuilds) the sparse line index on a background thread.
    /// </summary>
    /// <param name="source">
    /// The text source to scan.  The reference is kept so that
    /// <see cref="GetLineStartOffset"/> can do forward scans.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="progress">
    /// Optional progress reporter.  Reports values in the range [0.0, 1.0].
    /// </param>
    public Task BuildAsync(
        ITextSource source,
        CancellationToken ct = default,
        IProgress<double>? progress = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        return Task.Run(() => Build(source, ct, progress), ct);
    }

    /// <summary>
    /// Synchronous build — called on the background thread by <see cref="BuildAsync"/>.
    /// </summary>
    private void Build(ITextSource source, CancellationToken ct, IProgress<double>? progress)
    {
        long len = source.Length;

        // Worst-case estimate: every character is a '\n'.
        // We grow the list dynamically, so this is just a starting capacity.
        var entries = new List<long>(Math.Min((int)(len / _sampleInterval) + 2, 1 << 20));

        // Line 0 always starts at offset 0.
        entries.Add(0);

        long currentLine = 0;      // zero-based line counter
        long nextCheckpoint = _sampleInterval;
        long lastProgressChars = 0;

        const int progressGranularity = 1 << 16; // report every ~64K chars

        for (long i = 0; i < len; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (source[i] == '\n')
            {
                currentLine++;

                if (currentLine == nextCheckpoint)
                {
                    // Record the start of this line (character right after '\n').
                    entries.Add(i + 1);
                    nextCheckpoint += _sampleInterval;
                }
            }

            if (progress is not null && i - lastProgressChars >= progressGranularity)
            {
                lastProgressChars = i;
                progress.Report((double)i / len);
            }
        }

        // Total lines = currentLine + 1  (the last line, which may or may
        // not end with '\n', still counts).
        _totalLines = currentLine + 1;
        _entries = entries.ToArray();
        _source = source;

        progress?.Report(1.0);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Query
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the character offset where the given zero-based line starts.
    /// Uses a binary search to find the nearest checkpoint, then performs a
    /// short forward scan.
    /// </summary>
    /// <param name="lineNumber">Zero-based line number.</param>
    /// <returns>Character offset of the first character on that line.</returns>
    public long GetLineStartOffset(long lineNumber)
    {
        if (!IsBuilt)
            throw new InvalidOperationException("The index has not been built yet. Call BuildAsync first.");

        if (lineNumber < 0 || lineNumber >= _totalLines)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));

        if (lineNumber == 0)
            return 0;

        ITextSource source = _source!;

        // Find the largest checkpoint <= lineNumber.
        long bucketIndex = lineNumber / _sampleInterval;

        // Clamp to valid range (should always be valid, but be defensive).
        if (bucketIndex >= _entries.Length)
            bucketIndex = _entries.Length - 1;

        long checkpointLine = bucketIndex * _sampleInterval;
        long charOffset = _entries[bucketIndex];

        // Forward scan from the checkpoint to the target line.
        long linesToSkip = lineNumber - checkpointLine;
        long len = source.Length;

        while (linesToSkip > 0 && charOffset < len)
        {
            if (source[charOffset] == '\n')
                linesToSkip--;

            charOffset++;
        }

        return charOffset;
    }
}
