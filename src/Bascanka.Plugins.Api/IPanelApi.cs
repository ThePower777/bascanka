namespace Bascanka.Plugins.Api;

/// <summary>
/// Provides methods for registering and managing dockable tool panels
/// within the editor shell.
/// </summary>
public interface IPanelApi
{
    /// <summary>
    /// Registers a new panel with the editor.
    /// </summary>
    /// <param name="id">
    /// A unique identifier for the panel. Must be unique across all plugins.
    /// </param>
    /// <param name="title">The title displayed in the panel's header or tab.</param>
    /// <param name="control">
    /// The UI control to host inside the panel.
    /// This should be a <c>System.Windows.Forms.Control</c> instance.
    /// </param>
    void RegisterPanel(string id, string title, object control);

    /// <summary>
    /// Makes a previously registered panel visible.
    /// </summary>
    /// <param name="id">The unique identifier of the panel.</param>
    void ShowPanel(string id);

    /// <summary>
    /// Hides a previously registered panel without removing it.
    /// </summary>
    /// <param name="id">The unique identifier of the panel.</param>
    void HidePanel(string id);

    /// <summary>
    /// Removes a panel from the editor entirely. The panel's control
    /// will be disposed by the host.
    /// </summary>
    /// <param name="id">The unique identifier of the panel to remove.</param>
    void RemovePanel(string id);
}
