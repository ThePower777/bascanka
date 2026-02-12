namespace Bascanka.Plugins.Api;

/// <summary>
/// Event arguments for document lifecycle events (opened, closed, saved).
/// </summary>
public class DocumentEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of <see cref="DocumentEventArgs"/>.
    /// </summary>
    /// <param name="filePath">The file system path of the document.</param>
    public DocumentEventArgs(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the absolute file system path of the affected document.
    /// May be an empty string for documents that have never been saved.
    /// </summary>
    public string FilePath { get; }
}

/// <summary>
/// Event arguments raised when the text of a buffer changes.
/// </summary>
public class TextChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of <see cref="TextChangedEventArgs"/>.
    /// </summary>
    /// <param name="offset">The zero-based character offset where the change started.</param>
    /// <param name="oldLength">The number of characters that were removed.</param>
    /// <param name="newLength">The number of characters that were inserted.</param>
    public TextChangedEventArgs(long offset, long oldLength, long newLength)
    {
        Offset = offset;
        OldLength = oldLength;
        NewLength = newLength;
    }

    /// <summary>Gets the zero-based character offset where the change started.</summary>
    public long Offset { get; }

    /// <summary>Gets the number of characters that were removed.</summary>
    public long OldLength { get; }

    /// <summary>Gets the number of characters that were inserted.</summary>
    public long NewLength { get; }
}

/// <summary>
/// Event arguments for keyboard input events.
/// Named <c>KeyEventArgs2</c> to avoid conflicts with
/// <c>System.Windows.Forms.KeyEventArgs</c>.
/// </summary>
public class KeyEventArgs2 : EventArgs
{
    /// <summary>
    /// Initializes a new instance of <see cref="KeyEventArgs2"/>.
    /// </summary>
    /// <param name="keyCode">The virtual key code of the pressed key.</param>
    /// <param name="control">Whether the Ctrl modifier was held.</param>
    /// <param name="shift">Whether the Shift modifier was held.</param>
    /// <param name="alt">Whether the Alt modifier was held.</param>
    public KeyEventArgs2(int keyCode, bool control, bool shift, bool alt)
    {
        KeyCode = keyCode;
        Control = control;
        Shift = shift;
        Alt = alt;
    }

    /// <summary>Gets the virtual key code of the pressed key.</summary>
    public int KeyCode { get; }

    /// <summary>Gets a value indicating whether the Ctrl modifier was held.</summary>
    public bool Control { get; }

    /// <summary>Gets a value indicating whether the Shift modifier was held.</summary>
    public bool Shift { get; }

    /// <summary>Gets a value indicating whether the Alt modifier was held.</summary>
    public bool Alt { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the key event has been handled.
    /// Set to <c>true</c> to prevent the editor from processing the key.
    /// </summary>
    public bool Handled { get; set; }
}
