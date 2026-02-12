namespace Bascanka.Plugins.Api;

/// <summary>
/// Provides methods for displaying information in the editor's status bar.
/// Each plugin-owned field is identified by a unique string id.
/// </summary>
public interface IStatusBarApi
{
    /// <summary>
    /// Creates or updates a named field in the status bar.
    /// If the field does not yet exist it will be created; otherwise its
    /// text is updated in place.
    /// </summary>
    /// <param name="id">A unique identifier for the status bar field.</param>
    /// <param name="text">The text to display.</param>
    void SetField(string id, string text);

    /// <summary>
    /// Removes a previously created field from the status bar.
    /// Does nothing if the field does not exist.
    /// </summary>
    /// <param name="id">The identifier of the field to remove.</param>
    void RemoveField(string id);
}
