namespace Bascanka.Core.Buffer;

/// <summary>
/// Implemented by <see cref="ITextSource"/> instances that pre-compute the
/// total <c>'\n'</c> count and line offset table during construction
/// (e.g. memory-mapped sources).
/// <see cref="PieceTable"/> uses this to skip the initial full-file scan
/// and avoid building the line-offset cache on the UI thread.
/// </summary>
public interface IPrecomputedLineFeeds
{
    /// <summary>
    /// Total number of <c>'\n'</c> characters in the entire source,
    /// computed during construction.
    /// </summary>
    int InitialLineFeedCount { get; }

    /// <summary>
    /// Pre-built line-offset table where element <c>i</c> is the character
    /// offset of line <c>i</c>.  May be <see langword="null"/> if not available.
    /// Ownership transfers to the consumer; the source should not modify
    /// the array after handing it out.
    /// </summary>
    long[]? LineOffsets { get; }
}

/// <summary>
/// Abstraction over a read-only text store.  Implementations may be backed by
/// a plain <see cref="string"/>, a memory-mapped file, or any other source
/// that supports random character access.
/// </summary>
public interface ITextSource
{
    /// <summary>Returns the character at the given zero-based index.</summary>
    char this[long index] { get; }

    /// <summary>Total number of characters in the source.</summary>
    long Length { get; }

    /// <summary>
    /// Copies a contiguous range of characters into a new <see cref="string"/>.
    /// </summary>
    /// <param name="start">Zero-based start index (inclusive).</param>
    /// <param name="length">Number of characters to copy.</param>
    string GetText(long start, long length);

    /// <summary>
    /// Counts the number of <c>'\n'</c> characters in the given range.
    /// </summary>
    /// <param name="start">Zero-based start index (inclusive).</param>
    /// <param name="length">Number of characters to scan.</param>
    int CountLineFeeds(long start, long length);
}
