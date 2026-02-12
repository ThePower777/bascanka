using Microsoft.Win32;

namespace Bascanka.App;

/// <summary>
/// Manages application settings using the Windows Registry.
/// All values are stored under <c>HKEY_CURRENT_USER\Software\Bascanka</c>.
/// </summary>
internal static class SettingsManager
{
    private const string RegistryKeyPath = @"Software\Bascanka";

    /// <summary>Gets a string value from the registry, or the default if not found.</summary>
    public static string GetString(string name, string defaultValue = "")
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(name) as string ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>Sets a string value in the registry.</summary>
    public static void SetString(string name, string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue(name, value, RegistryValueKind.String);
        }
        catch
        {
            // Silently ignore if registry access fails.
        }
    }

    /// <summary>Gets a boolean value from the registry.</summary>
    public static bool GetBool(string name, bool defaultValue = false)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key?.GetValue(name) is int intVal)
                return intVal != 0;
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>Sets a boolean value in the registry (stored as DWORD 0/1).</summary>
    public static void SetBool(string name, bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
            // Silently ignore.
        }
    }

    /// <summary>Gets an integer value from the registry.</summary>
    public static int GetInt(string name, int defaultValue = 0)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key?.GetValue(name) is int intVal)
                return intVal;
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>Sets an integer value in the registry.</summary>
    public static void SetInt(string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch
        {
            // Silently ignore.
        }
    }

    // ── Well-known setting names ────────────────────────────────────

    public const string KeyLanguage = "Language";
    public const string KeyWordWrap = "WordWrap";
    public const string KeyShowWhitespace = "ShowWhitespace";
    public const string KeyShowLineNumbers = "ShowLineNumbers";
    public const string KeyTheme = "Theme";

    // ── Explorer context menu ───────────────────────────────────────

    private const string ExplorerContextKeyPath = @"*\shell\Bascanka";
    private const string ExplorerCommandKeyPath = @"*\shell\Bascanka\command";

    /// <summary>
    /// Returns whether the "Edit with Bascanka" context menu entry is registered.
    /// </summary>
    public static bool IsExplorerContextMenuRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\" + ExplorerContextKeyPath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers "Edit with Bascanka" in the Windows Explorer right-click
    /// context menu for all file types. Uses HKCU so no admin rights needed.
    /// </summary>
    public static void RegisterExplorerContextMenu()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;

            using var shellKey = Registry.CurrentUser.CreateSubKey(
                @"Software\Classes\" + ExplorerContextKeyPath);
            shellKey.SetValue("", "Edit with Bascanka");
            shellKey.SetValue("Icon", $"\"{exePath}\",0");

            using var cmdKey = Registry.CurrentUser.CreateSubKey(
                @"Software\Classes\" + ExplorerCommandKeyPath);
            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // Silently ignore.
        }
    }

    /// <summary>
    /// Removes the "Edit with Bascanka" context menu entry from Explorer.
    /// </summary>
    public static void UnregisterExplorerContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Classes\" + ExplorerContextKeyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // Silently ignore.
        }
    }

}
