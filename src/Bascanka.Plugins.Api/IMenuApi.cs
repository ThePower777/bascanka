namespace Bascanka.Plugins.Api;

/// <summary>
/// Provides methods for extending the editor's menu bar with
/// custom menu items and separators.
/// </summary>
public interface IMenuApi
{
    /// <summary>
    /// Adds a new menu item under an existing top-level menu.
    /// </summary>
    /// <param name="parentMenu">
    /// The name of the parent menu (e.g. "File", "Edit", "Plugins").
    /// If the parent menu does not exist it will be created automatically.
    /// </param>
    /// <param name="text">The display text of the menu item.</param>
    /// <param name="onClick">The action invoked when the item is clicked.</param>
    /// <param name="shortcut">
    /// An optional keyboard shortcut string (e.g. "Ctrl+Shift+P").
    /// </param>
    void AddMenuItem(string parentMenu, string text, Action onClick, string? shortcut = null);

    /// <summary>
    /// Adds a visual separator line to the specified parent menu.
    /// </summary>
    /// <param name="parentMenu">The name of the parent menu.</param>
    void AddSeparator(string parentMenu);

    /// <summary>
    /// Removes a previously added menu item by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the menu item to remove.</param>
    void RemoveMenuItem(string id);
}
