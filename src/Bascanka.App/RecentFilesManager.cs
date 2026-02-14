using System.Text.Json;

namespace Bascanka.App;

/// <summary>
/// Manages the most-recently-used (MRU) file list. Persists up to
/// <see cref="MaxRecentFiles"/> entries to a JSON file in the user's
/// AppData folder.
/// </summary>
public sealed class RecentFilesManager
{
    /// <summary>Maximum number of recent files to retain.</summary>
    public static int MaxRecentFiles { get; set; } = 20;

    private static readonly string DataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bascanka");

    private static readonly string RecentFilePath =
        Path.Combine(DataDirectory, "recent.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly List<string> _recentFiles;

    public RecentFilesManager()
    {
        _recentFiles = Load();
    }

    /// <summary>
    /// Adds a file path to the top of the recent files list.
    /// If the path already exists, it is moved to the top.
    /// The list is truncated to <see cref="MaxRecentFiles"/> entries.
    /// </summary>
    public void AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string fullPath = Path.GetFullPath(path);

        // Remove if already present (to move to top).
        _recentFiles.RemoveAll(f =>
            string.Equals(f, fullPath, StringComparison.OrdinalIgnoreCase));

        // Insert at the beginning.
        _recentFiles.Insert(0, fullPath);

        // Trim to maximum.
        while (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveAt(_recentFiles.Count - 1);

        Save();
    }

    /// <summary>
    /// Returns the list of recent file paths, most recent first.
    /// </summary>
    public IReadOnlyList<string> GetRecentFiles()
    {
        return _recentFiles.AsReadOnly();
    }

    /// <summary>
    /// Clears the entire recent files list and deletes the persisted file.
    /// </summary>
    public void ClearRecentFiles()
    {
        _recentFiles.Clear();
        Save();
    }

    // ── Persistence ──────────────────────────────────────────────────

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            string json = JsonSerializer.Serialize(_recentFiles, JsonOptions);
            File.WriteAllText(RecentFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save recent files: {ex.Message}");
        }
    }

    private static List<string> Load()
    {
        try
        {
            if (!File.Exists(RecentFilePath))
                return new List<string>();

            string json = File.ReadAllText(RecentFilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load recent files: {ex.Message}");
            return new List<string>();
        }
    }
}
