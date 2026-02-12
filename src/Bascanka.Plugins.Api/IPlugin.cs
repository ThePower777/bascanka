namespace Bascanka.Plugins.Api;

/// <summary>
/// Entry point that every Bascanka plugin must implement.
/// The editor discovers types that implement this interface
/// when it loads a plugin assembly.
/// </summary>
public interface IPlugin
{
    /// <summary>Gets the human-readable name of the plugin.</summary>
    string Name { get; }

    /// <summary>Gets the semantic version string of the plugin (e.g. "1.0.0").</summary>
    string Version { get; }

    /// <summary>Gets the author or organization that created the plugin.</summary>
    string Author { get; }

    /// <summary>Gets a short description of what the plugin does.</summary>
    string Description { get; }

    /// <summary>
    /// Called once when the editor loads the plugin.
    /// Use this to register menus, panels, key bindings, and event handlers.
    /// </summary>
    /// <param name="host">
    /// The host facade that exposes editor services to the plugin.
    /// </param>
    void Initialize(IEditorHost host);

    /// <summary>
    /// Called when the editor unloads the plugin or shuts down.
    /// Release any resources and unsubscribe from events here.
    /// </summary>
    void Shutdown();
}
