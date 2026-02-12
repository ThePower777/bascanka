namespace Bascanka.Core.Search;

/// <summary>
/// Represents a single match found by the search engine, including its
/// position within the document (or file), length, and contextual line text.
/// </summary>
public sealed class SearchResult
{
    /// <summary>
    /// Zero-based character offset of the match within the document.
    /// </summary>
    public long Offset { get; init; }

    /// <summary>
    /// Number of characters in the matched text.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// One-based line number on which the match begins.
    /// </summary>
    public long LineNumber { get; init; }

    /// <summary>
    /// One-based column number at which the match begins within its line.
    /// </summary>
    public int ColumnNumber { get; init; }

    /// <summary>
    /// The full text of the line that contains the match.
    /// Useful for displaying search results with surrounding context.
    /// </summary>
    public string LineText { get; init; } = string.Empty;

    /// <summary>
    /// The file path that contains this match.  Populated for Find-in-Files
    /// results; <see langword="null"/> for single-document searches.
    /// </summary>
    public string? FilePath { get; init; }

    public override string ToString() =>
        FilePath is not null
            ? $"{FilePath}({LineNumber},{ColumnNumber}): [{Length}] {LineText.Trim()}"
            : $"({LineNumber},{ColumnNumber}): [{Length}] {LineText.Trim()}";
}
