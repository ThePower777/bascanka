namespace Bascanka.App;

/// <summary>
/// Saves and restores the editor session (open tabs, per-tab state, window geometry)
/// across application launches. Session data is persisted to the Windows Registry
/// under <c>HKCU\Software\Bascanka\Session</c>.
/// </summary>
public sealed class SessionManager
{
    /// <summary>
    /// Saves the current session state from the main form.
    /// Records window geometry, all open file-backed tabs, and per-tab
    /// zoom/scroll/caret state.
    /// </summary>
    public void SaveSession(MainForm form)
    {
        try
        {
            // Clear old session data first.
            SettingsManager.ClearSessionState();

            // ── Window geometry ──────────────────────────────────────
            bool maximized = form.WindowState == FormWindowState.Maximized;
            var bounds = maximized ? form.RestoreBounds : form.Bounds;

            SettingsManager.SetSessionInt("WindowX", bounds.X);
            SettingsManager.SetSessionInt("WindowY", bounds.Y);
            SettingsManager.SetSessionInt("WindowWidth", bounds.Width);
            SettingsManager.SetSessionInt("WindowHeight", bounds.Height);
            SettingsManager.SetSessionInt("WindowMaximized", maximized ? 1 : 0);

            // ── Tabs ─────────────────────────────────────────────────
            int savedIndex = 0;
            int activeTabIndex = form.ActiveTabIndex;
            int savedActiveIndex = -1;

            foreach (var tab in form.Tabs)
            {
                // Only persist file-backed tabs (untitled documents are not saved).
                if (tab.FilePath is null) continue;

                SettingsManager.SetSessionString($"Tab{savedIndex}_Path", tab.FilePath);

                if (tab.IsDeferredLoad)
                {
                    // Tab was never activated — preserve the pending state.
                    SettingsManager.SetSessionInt($"Tab{savedIndex}_Zoom", tab.PendingZoom);
                    SettingsManager.SetSessionInt($"Tab{savedIndex}_Scroll", tab.PendingScroll);
                    SettingsManager.SetSessionInt($"Tab{savedIndex}_Caret", (int)Math.Min(tab.PendingCaret, int.MaxValue));
                }
                else
                {
                    SettingsManager.SetSessionInt($"Tab{savedIndex}_Zoom", tab.Editor.ZoomLevel);
                    SettingsManager.SetSessionInt($"Tab{savedIndex}_Scroll", (int)tab.Editor.ScrollMgr.FirstVisibleLine);
                    SettingsManager.SetSessionInt($"Tab{savedIndex}_Caret", (int)Math.Min(tab.Editor.CaretOffset, int.MaxValue));
                }

                int originalIndex = ((IList<Editor.Tabs.TabInfo>)form.Tabs).IndexOf(tab);
                if (originalIndex == activeTabIndex)
                    savedActiveIndex = savedIndex;

                savedIndex++;
            }

            SettingsManager.SetSessionInt("TabCount", savedIndex);
            SettingsManager.SetSessionInt("ActiveTab", savedActiveIndex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores the previous session by opening all saved file-backed tabs
    /// and applying per-tab zoom/scroll/caret state.
    /// Returns true if at least one file was restored.
    /// </summary>
    public bool RestoreSession(MainForm form)
    {
        try
        {
            int tabCount = SettingsManager.GetSessionInt("TabCount", 0);
            if (tabCount <= 0) return false;

            int activeIndex = SettingsManager.GetSessionInt("ActiveTab", -1);
            if (activeIndex < 0 || activeIndex >= tabCount)
                activeIndex = 0;

            // ── Create all tabs ──────────────────────────────────────
            // Only the active tab is loaded eagerly; others are deferred.
            int actualActiveIndex = -1;

            for (int i = 0; i < tabCount; i++)
            {
                string path = SettingsManager.GetSessionString($"Tab{i}_Path");
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                int zoom = SettingsManager.GetSessionInt($"Tab{i}_Zoom", 0);
                int scroll = SettingsManager.GetSessionInt($"Tab{i}_Scroll", 0);
                int caret = SettingsManager.GetSessionInt($"Tab{i}_Caret", 0);

                if (i == activeIndex)
                {
                    // Eagerly load the active tab.
                    form.OpenFile(path);
                    actualActiveIndex = form.Tabs.Count - 1;

                    // Store state as pending — applied in ActivateTab.
                    var tab = form.Tabs[actualActiveIndex];
                    if (string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        tab.PendingZoom = zoom;
                        tab.PendingScroll = scroll;
                        tab.PendingCaret = caret;
                    }
                }
                else
                {
                    // Defer loading for inactive tabs.
                    form.AddDeferredTab(path, zoom, scroll, caret);
                }
            }

            // Activate the previously active tab (applies pending state).
            if (actualActiveIndex >= 0)
                form.ActivateTab(actualActiveIndex);
            else if (form.Tabs.Count > 0)
                form.ActivateTab(0);

            return form.Tabs.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore session: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores window geometry from a previous session. Should be called
    /// during form construction, before <see cref="Form.OnLoad"/>.
    /// </summary>
    public void RestoreWindowState(MainForm form)
    {
        try
        {
            int x = SettingsManager.GetSessionInt("WindowX", int.MinValue);
            int y = SettingsManager.GetSessionInt("WindowY", int.MinValue);
            int w = SettingsManager.GetSessionInt("WindowWidth", 0);
            int h = SettingsManager.GetSessionInt("WindowHeight", 0);
            bool maximized = SettingsManager.GetSessionInt("WindowMaximized", 0) != 0;

            if (w <= 0 || h <= 0) return;

            // Validate that the saved position is at least partially on-screen.
            var savedRect = new System.Drawing.Rectangle(x, y, w, h);
            bool onScreen = false;
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(savedRect))
                {
                    onScreen = true;
                    break;
                }
            }

            if (!onScreen) return; // Saved position is off-screen — keep defaults.

            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(x, y);
            form.Size = new System.Drawing.Size(w, h);

            if (maximized)
                form.WindowState = FormWindowState.Maximized;
        }
        catch
        {
            // Silently ignore — keep default window position.
        }
    }

    /// <summary>
    /// Clears all session state from the registry.
    /// </summary>
    public void ClearSession()
    {
        SettingsManager.ClearSessionState();
    }
}
