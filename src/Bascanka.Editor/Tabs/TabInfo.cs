using Bascanka.Editor.Controls;

namespace Bascanka.Editor.Tabs;

/// <summary>
/// Holds the metadata and editor reference for a single open tab in the editor.
/// Each tab is uniquely identified by its <see cref="Id"/> and tracks whether
/// its content has been modified since the last save.
/// </summary>
public sealed class TabInfo
{
    /// <summary>
    /// Unique, immutable identifier for this tab instance.  Generated once at
    /// construction and never changes, even if the tab is reordered or renamed.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Display title shown on the tab strip.  Typically the file name (without
    /// path) for file-backed tabs, or a placeholder such as "Untitled 1" for
    /// new, unsaved documents.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the file on disk, or <see langword="null"/> if the document
    /// has never been saved.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Indicates whether the document content has been modified since its last
    /// save.  When <see langword="true"/>, the tab strip renders a modified
    /// indicator (e.g. a dot or asterisk) alongside the title.
    /// </summary>
    public bool IsModified { get; set; }

    /// <summary>
    /// The <see cref="EditorControl"/> that provides the editing surface for
    /// this tab's document.
    /// </summary>
    public required EditorControl Editor { get; set; }

    /// <summary>
    /// Indicates that this tab is showing a binary file in hex-only mode.
    /// Binary tabs cannot be saved as text.
    /// </summary>
    public bool IsBinaryMode { get; set; }

    /// <summary>
    /// Arbitrary user data associated with this tab.  Consumers may use this
    /// property to attach additional context (plugin state, document metadata,
    /// etc.) without subclassing <see cref="TabInfo"/>.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Returns the display title, including a modified indicator when applicable.
    /// </summary>
    public string DisplayTitle => IsModified ? $"* {Title}" : Title;

    /// <inheritdoc/>
    public override string ToString() => DisplayTitle;
}
