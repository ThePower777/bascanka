using System.Text.Json;
using System.Text.Json.Serialization;
using Bascanka.Editor.Tabs;

namespace Bascanka.App;

/// <summary>
/// Saves and restores the editor session (list of open files and their state)
/// across application launches. Session data is persisted to a JSON file in
/// the user's AppData folder.
/// </summary>
public sealed class SessionManager
{
    private static readonly string SessionDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bascanka");

    private static readonly string SessionFilePath =
        Path.Combine(SessionDirectory, "session.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Saves the current session state from the main form.
    /// Records all open file-backed tabs, their scroll positions, cursor offsets,
    /// and which tab is active.
    /// </summary>
    public void SaveSession(MainForm form)
    {
        try
        {
            var data = new SessionData
            {
                ActiveTabIndex = form.ActiveTabIndex,
                Tabs = new List<SessionTab>(),
            };

            foreach (var tab in form.Tabs)
            {
                // Only persist file-backed tabs (untitled documents are not saved).
                if (tab.FilePath is null) continue;

                data.Tabs.Add(new SessionTab
                {
                    FilePath = tab.FilePath,
                    ScrollPosition = 0, // Will be populated when EditorControl exposes scroll state.
                    CursorOffset = 0,   // Will be populated when EditorControl exposes caret offset.
                    IsActive = ((IList<TabInfo>)form.Tabs).IndexOf(tab) == form.ActiveTabIndex,
                });
            }

            Directory.CreateDirectory(SessionDirectory);
            string json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(SessionFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores the previous session by opening all saved file-backed tabs.
    /// Returns true if at least one file was restored.
    /// </summary>
    public bool RestoreSession(MainForm form)
    {
        if (!File.Exists(SessionFilePath))
            return false;

        try
        {
            string json = File.ReadAllText(SessionFilePath);
            var data = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);

            if (data?.Tabs is null || data.Tabs.Count == 0)
                return false;

            int activeIndex = -1;
            int tabIndex = 0;

            foreach (SessionTab tabData in data.Tabs)
            {
                if (string.IsNullOrEmpty(tabData.FilePath) || !File.Exists(tabData.FilePath))
                    continue;

                form.OpenFile(tabData.FilePath);

                if (tabData.IsActive)
                    activeIndex = tabIndex;

                tabIndex++;
            }

            // Activate the previously active tab.
            if (activeIndex >= 0 && activeIndex < form.Tabs.Count)
                form.ActivateTab(activeIndex);

            return tabIndex > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore session: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes the session file.
    /// </summary>
    public void ClearSession()
    {
        try
        {
            if (File.Exists(SessionFilePath))
                File.Delete(SessionFilePath);
        }
        catch
        {
            // Ignore errors during cleanup.
        }
    }
}

/// <summary>
/// Serializable data structure representing the full session state.
/// </summary>
public sealed class SessionData
{
    [JsonPropertyName("activeTabIndex")]
    public int ActiveTabIndex { get; set; }

    [JsonPropertyName("tabs")]
    public List<SessionTab> Tabs { get; set; } = new();
}

/// <summary>
/// Serializable data for a single tab in the session.
/// </summary>
public sealed class SessionTab
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("scrollPosition")]
    public long ScrollPosition { get; set; }

    [JsonPropertyName("cursorOffset")]
    public long CursorOffset { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}
