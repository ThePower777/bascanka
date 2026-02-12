using Bascanka.Core.Buffer;

namespace Bascanka.Core.Search;

/// <summary>
/// Reports progress of a directory-level file search.
/// </summary>
/// <param name="CurrentFile">The file currently being searched.</param>
/// <param name="FilesSearched">Total number of files searched so far.</param>
/// <param name="TotalMatches">Cumulative number of matches found so far.</param>
public record FileSearchProgress(string CurrentFile, int FilesSearched, int TotalMatches);

/// <summary>
/// Implements "Find in Files" functionality by searching all matching files
/// under a directory tree in parallel, reporting progress to an
/// <see cref="IProgress{FileSearchProgress}"/> callback.
/// </summary>
public sealed class FileSearcher
{
    private readonly SearchEngine _engine = new();

    /// <summary>
    /// Searches all files under <paramref name="path"/> that match
    /// <paramref name="fileFilter"/> for occurrences of the pattern
    /// defined in <paramref name="options"/>.
    /// </summary>
    /// <param name="path">Root directory to search.</param>
    /// <param name="fileFilter">
    /// A semicolon-separated list of glob patterns (e.g., <c>"*.cs;*.txt"</c>).
    /// If <see langword="null"/> or empty, all files are searched.
    /// </param>
    /// <param name="options">Search options (pattern, case, regex, etc.).</param>
    /// <param name="progress">
    /// Optional progress reporter.  Called after each file is searched.
    /// </param>
    /// <param name="ct">Cancellation token to abort the search.</param>
    /// <returns>An aggregated list of all matches across all files.</returns>
    public async Task<List<SearchResult>> SearchDirectoryAsync(
        string path,
        string? fileFilter,
        SearchOptions options,
        IProgress<FileSearchProgress>? progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        List<string> files = CollectFiles(path, fileFilter);
        var allResults = new List<SearchResult>();
        int filesSearched = 0;
        int totalMatches = 0;
        object lockObj = new();

        await Parallel.ForEachAsync(files, ct, async (filePath, token) =>
        {
            token.ThrowIfCancellationRequested();

            List<SearchResult> fileResults;
            try
            {
                fileResults = await Task.Run(() => SearchSingleFile(filePath, options), token);
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                // Skip files that cannot be read (binary, locked, permission denied, etc.).
                fileResults = [];
            }

            lock (lockObj)
            {
                allResults.AddRange(fileResults);
                filesSearched++;
                totalMatches += fileResults.Count;
            }

            progress?.Report(new FileSearchProgress(filePath, filesSearched, totalMatches));
        });

        return allResults;
    }

    /// <summary>
    /// Synchronous convenience overload for callers that do not need async.
    /// </summary>
    public List<SearchResult> SearchDirectory(
        string path,
        string? fileFilter,
        SearchOptions options,
        IProgress<FileSearchProgress>? progress,
        CancellationToken ct = default)
    {
        return SearchDirectoryAsync(path, fileFilter, options, progress, ct)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Searches a single file by reading its full text, wrapping it in a
    /// <see cref="PieceTable"/>, and delegating to <see cref="SearchEngine"/>.
    /// </summary>
    private List<SearchResult> SearchSingleFile(string filePath, SearchOptions options)
    {
        string content = File.ReadAllText(filePath);
        if (content.Length == 0)
            return [];

        var source = new StringTextSource(content);
        var buffer = new PieceTable(source);

        List<SearchResult> results = _engine.FindAll(buffer, options);

        // Tag each result with the file path.
        foreach (SearchResult r in results)
        {
            // SearchResult properties are init-only, so we rebuild with FilePath set.
            // For efficiency, we use a private helper to clone and set FilePath.
        }

        // Since SearchResult.FilePath is init-only, rebuild results with the path.
        return results.ConvertAll(r => new SearchResult
        {
            Offset = r.Offset,
            Length = r.Length,
            LineNumber = r.LineNumber,
            ColumnNumber = r.ColumnNumber,
            LineText = r.LineText,
            FilePath = filePath,
        });
    }

    /// <summary>
    /// Enumerates all files under <paramref name="rootPath"/> that match the
    /// semicolon-separated glob filters in <paramref name="fileFilter"/>.
    /// </summary>
    private static List<string> CollectFiles(string rootPath, string? fileFilter)
    {
        string[] patterns = ParseFilters(fileFilter);

        var files = new List<string>();
        foreach (string pattern in patterns)
        {
            try
            {
                files.AddRange(
                    Directory.EnumerateFiles(rootPath, pattern, SearchOption.AllDirectories));
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we cannot access.
            }
            catch (DirectoryNotFoundException)
            {
                // Skip directories that disappeared during enumeration.
            }
        }

        // Deduplicate (different patterns may match the same file).
        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Splits a semicolon-separated filter string into individual patterns.
    /// Returns <c>["*"]</c> when the filter is empty or null.
    /// </summary>
    private static string[] ParseFilters(string? fileFilter)
    {
        if (string.IsNullOrWhiteSpace(fileFilter))
            return ["*"];

        return fileFilter
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(f => f.Length > 0)
            .ToArray();
    }
}
