namespace Bascanka.Core.Search;

/// <summary>
/// Defines the scope over which a search is performed.
/// </summary>
public enum SearchScope
{
    /// <summary>Search within the currently active document only.</summary>
    CurrentDocument,

    /// <summary>Search across all documents that are currently open.</summary>
    AllOpenDocuments,

    /// <summary>Search all files under a specified directory path.</summary>
    Directory,
}

/// <summary>
/// Encapsulates all configurable options for a find/replace operation,
/// including pattern text, matching modes, direction, scope, and
/// directory-level search parameters.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>
    /// The search pattern -- either a literal string or a regular-expression
    /// pattern depending on <see cref="UseRegex"/>.
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// When <see langword="true"/>, <see cref="Pattern"/> is interpreted as a
    /// .NET regular expression.  When <see langword="false"/>, it is treated as
    /// a literal string.
    /// </summary>
    public bool UseRegex { get; init; }

    /// <summary>
    /// When <see langword="true"/>, matching is case-sensitive.
    /// </summary>
    public bool MatchCase { get; init; }

    /// <summary>
    /// When <see langword="true"/>, matches are restricted to whole words
    /// (bounded by non-word characters).
    /// </summary>
    public bool WholeWord { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the search wraps around to the beginning
    /// (or end, if searching up) of the document when no more matches are found.
    /// </summary>
    public bool WrapAround { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the search proceeds backward (toward the
    /// beginning of the document).
    /// </summary>
    public bool SearchUp { get; init; }

    /// <summary>
    /// Determines the scope of the search operation.
    /// </summary>
    public SearchScope Scope { get; init; } = SearchScope.CurrentDocument;

    /// <summary>
    /// The root directory for a <see cref="SearchScope.Directory"/> search.
    /// Ignored for other scopes.
    /// </summary>
    public string? DirectoryPath { get; init; }

    /// <summary>
    /// A semicolon-separated list of file-glob filters for directory searches
    /// (e.g., <c>"*.cs;*.txt"</c>).  Ignored for non-directory scopes.
    /// </summary>
    public string? FileFilter { get; init; }
}
