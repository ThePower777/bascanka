namespace Bascanka.Plugins.Api;

/// <summary>
/// Main host interface provided to plugins during initialization.
/// Grants access to the editor's sub-system APIs and global events.
/// </summary>
public interface IEditorHost
{
    /// <summary>Gets the API for extending the editor's menu bar.</summary>
    IMenuApi Menu { get; }

    /// <summary>Gets the API for registering and managing tool panels.</summary>
    IPanelApi Panels { get; }

    /// <summary>
    /// Gets the text buffer of the currently active document.
    /// May throw <see cref="InvalidOperationException"/> when no document is open.
    /// </summary>
    IBufferApi ActiveBuffer { get; }

    /// <summary>Gets the API for opening, saving, and switching documents.</summary>
    IDocumentApi Documents { get; }

    /// <summary>Gets the API for writing to the status bar.</summary>
    IStatusBarApi StatusBar { get; }

    /// <summary>Raised after a document is opened in the editor.</summary>
    event EventHandler<DocumentEventArgs> DocumentOpened;

    /// <summary>Raised after a document is closed.</summary>
    event EventHandler<DocumentEventArgs> DocumentClosed;

    /// <summary>Raised after a document is saved to disk.</summary>
    event EventHandler<DocumentEventArgs> DocumentSaved;

    /// <summary>Raised whenever the text of the active buffer changes.</summary>
    event EventHandler<TextChangedEventArgs> TextChanged;

    /// <summary>
    /// Raised when a key is pressed in the editor.
    /// Set <see cref="KeyEventArgs2.Handled"/> to <c>true</c> to suppress
    /// default handling.
    /// </summary>
    event EventHandler<KeyEventArgs2> KeyDown;

    /// <summary>
    /// Shows an informational message to the user (e.g. a toast or message box).
    /// </summary>
    /// <param name="message">The message text to display.</param>
    void ShowMessage(string message);

    /// <summary>
    /// Shows a modal input dialog and returns the text entered by the user,
    /// or <c>null</c> if the dialog was cancelled.
    /// </summary>
    /// <param name="prompt">The prompt text displayed in the dialog.</param>
    /// <param name="defaultValue">
    /// An optional default value pre-filled in the input field.
    /// </param>
    /// <returns>
    /// The text entered by the user, or <c>null</c> if the user cancelled.
    /// </returns>
    string? ShowInputDialog(string prompt, string defaultValue = "");
}
