namespace Bascanka.Plugins.Api;

/// <summary>
/// Provides methods for managing the set of open documents in the editor.
/// </summary>
public interface IDocumentApi
{
    /// <summary>
    /// Opens a file from disk in a new editor tab. If the file is already
    /// open, its tab is activated instead.
    /// </summary>
    /// <param name="path">The absolute file system path to open.</param>
    void OpenFile(string path);

    /// <summary>
    /// Creates a new untitled document in a fresh editor tab.
    /// </summary>
    void NewDocument();

    /// <summary>
    /// Saves the currently active document to its existing file path.
    /// If the document has never been saved, the editor will prompt the
    /// user for a path.
    /// </summary>
    void SaveActiveDocument();

    /// <summary>
    /// Saves the currently active document to the specified path,
    /// updating its associated file path going forward.
    /// </summary>
    /// <param name="path">The absolute file system path to save to.</param>
    void SaveActiveDocumentAs(string path);

    /// <summary>
    /// Gets the file path of the active document, or <c>null</c> if the
    /// document has not been saved to disk yet.
    /// </summary>
    string? ActiveDocumentPath { get; }

    /// <summary>
    /// Gets the file paths of all currently open documents. Unsaved
    /// documents appear as empty strings in the list.
    /// </summary>
    IReadOnlyList<string> OpenDocumentPaths { get; }

    /// <summary>
    /// Activates (brings to front) the document at the given index in the
    /// <see cref="OpenDocumentPaths"/> list.
    /// </summary>
    /// <param name="index">The zero-based index of the document to activate.</param>
    void ActivateDocument(int index);
}
