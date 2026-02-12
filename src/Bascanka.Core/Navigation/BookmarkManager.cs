namespace Bascanka.Core.Navigation;

/// <summary>
/// Manages a set of bookmarked line numbers for a document.  Supports toggling,
/// querying, and forward/backward navigation between bookmarks.
/// </summary>
public sealed class BookmarkManager
{
    private readonly SortedSet<long> _bookmarks = new();

    /// <summary>
    /// Raised whenever the bookmark set changes (add, remove, or clear).
    /// </summary>
    public event EventHandler? BookmarksChanged;

    /// <summary>
    /// Toggles the bookmark on the specified <paramref name="line"/>.
    /// If the line is already bookmarked it is removed; otherwise it is added.
    /// </summary>
    /// <param name="line">One-based line number.</param>
    public void Toggle(long line)
    {
        if (!_bookmarks.Remove(line))
            _bookmarks.Add(line);

        OnBookmarksChanged();
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given <paramref name="line"/> is bookmarked.
    /// </summary>
    /// <param name="line">One-based line number.</param>
    public bool IsBookmarked(long line) => _bookmarks.Contains(line);

    /// <summary>
    /// Returns the next bookmarked line after <paramref name="currentLine"/>,
    /// or the first bookmark if we wrap around.  Returns <see langword="null"/>
    /// if there are no bookmarks.
    /// </summary>
    /// <param name="currentLine">One-based line number of the caret.</param>
    public long? NextBookmark(long currentLine)
    {
        if (_bookmarks.Count == 0)
            return null;

        // GetViewBetween returns the subset > currentLine.
        foreach (long line in _bookmarks)
        {
            if (line > currentLine)
                return line;
        }

        // Wrap around to the first bookmark.
        return _bookmarks.Min;
    }

    /// <summary>
    /// Returns the previous bookmarked line before <paramref name="currentLine"/>,
    /// or the last bookmark if we wrap around.  Returns <see langword="null"/>
    /// if there are no bookmarks.
    /// </summary>
    /// <param name="currentLine">One-based line number of the caret.</param>
    public long? PreviousBookmark(long currentLine)
    {
        if (_bookmarks.Count == 0)
            return null;

        // Iterate in reverse to find the first bookmark < currentLine.
        foreach (long line in _bookmarks.Reverse())
        {
            if (line < currentLine)
                return line;
        }

        // Wrap around to the last bookmark.
        return _bookmarks.Max;
    }

    /// <summary>
    /// Removes all bookmarks.
    /// </summary>
    public void ClearAll()
    {
        if (_bookmarks.Count == 0)
            return;

        _bookmarks.Clear();
        OnBookmarksChanged();
    }

    /// <summary>
    /// Returns a read-only snapshot of all bookmarked line numbers, sorted
    /// in ascending order.
    /// </summary>
    public IReadOnlyList<long> GetAll() => _bookmarks.ToList().AsReadOnly();

    /// <summary>
    /// Returns the number of bookmarks currently set.
    /// </summary>
    public int Count => _bookmarks.Count;

    private void OnBookmarksChanged() => BookmarksChanged?.Invoke(this, EventArgs.Empty);
}
