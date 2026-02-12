using System.Text.RegularExpressions;
using Bascanka.Core.Buffer;
using Bascanka.Core.Search;
using Bascanka.Editor.Controls;
using Bascanka.Editor.Themes;

namespace Bascanka.Editor.Panels;

/// <summary>
/// A modeless find/replace panel anchored to the top-right of the editor area,
/// styled similarly to VS Code's search widget.
/// </summary>
public class FindReplacePanel : UserControl
{
    // ── Constants ─────────────────────────────────────────────────────
    private const int MaxHistoryItems = 25;
    private const int DebounceMsec = 300;
    private const int PanelWidth = 520;
    private const int FindRowHeight = 40;
    private const int ReplaceRowHeight = 40;
    private const int PanelPadding = 8;

    // ── Controls: Find row ────────────────────────────────────────────
    private readonly ComboBox _searchBox;
    private readonly Button _btnMatchCase;
    private readonly Button _btnWholeWord;
    private readonly Button _btnRegex;
    private readonly Button _btnFindNext;
    private readonly Button _btnFindPrev;
    private readonly Button _btnCount;
    private readonly Button _btnMarkAll;
    private readonly Button _btnFindAll;
    private readonly Button _btnFindAllTabs;
    private readonly Label _statusLabel;
    private readonly Button _btnClose;

    // ── Controls: Replace row ─────────────────────────────────────────
    private readonly TextBox _replaceBox;
    private readonly Button _btnReplace;
    private readonly Button _btnReplaceAll;
    private readonly Button _btnExpandReplace;
    private readonly Panel _replaceRow;

    // ── State ─────────────────────────────────────────────────────────
    private readonly SearchEngine _searchEngine = new();
    private readonly List<string> _searchHistory = [];
    private readonly System.Windows.Forms.Timer _debounceTimer;
    private CancellationTokenSource? _searchCts;
    private bool _matchCase;
    private bool _wholeWord;
    private bool _useRegex;
    private bool _replaceVisible;
    private int _currentMatchIndex;
    private List<SearchResult> _currentMatches = [];

    // ── External references ───────────────────────────────────────────
    private EditorControl? _editor;
    private PieceTable? _buffer;
    private ITheme? _theme;

    // ── Events ────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks the Close button or presses Escape.</summary>
    public event EventHandler? PanelClosed;

    /// <summary>Raised when the user navigates to a match (Find Next/Prev).</summary>
    public event EventHandler<SearchResult>? NavigateToMatch;

    /// <summary>Raised when the search highlight pattern changes (for viewport rendering).</summary>
    public event EventHandler<Regex?>? MatchesHighlighted;

    /// <summary>Raised when Find Next/Previous needs a background search with progress (large files).</summary>
    public event EventHandler<FindNextRequestEventArgs>? FindNextRequested;

    /// <summary>Raised when "Find All" is clicked, requesting MainForm to run the search with progress.</summary>
    public event EventHandler<SearchOptions>? FindAllRequested;

    /// <summary>Raised when "Find All in Tabs" is clicked, requesting a multi-tab search.</summary>
    public event EventHandler<SearchOptions>? FindAllInTabsRequested;

    // ── Construction ──────────────────────────────────────────────────

    public FindReplacePanel()
    {
        // Right-aligned, not full-width docked.
        Dock = DockStyle.Top;
        Height = FindRowHeight + 30 + PanelPadding * 2;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        // ── Debounce timer ────────────────────────────────────────────
        _debounceTimer = new System.Windows.Forms.Timer { Interval = DebounceMsec };
        _debounceTimer.Tick += OnDebounceTick;

        // ── Search box (combo for history) ────────────────────────────
        _searchBox = new ComboBox
        {
            Width = 220,
            Height = 26,
            DropDownStyle = ComboBoxStyle.DropDown,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
        };
        _searchBox.TextChanged += (_, _) => RestartDebounce();
        _searchBox.KeyDown += OnSearchBoxKeyDown;

        // ── Toggle buttons ────────────────────────────────────────────
        _btnMatchCase = CreateToggleButton("Aa", "Match Case");
        _btnMatchCase.Click += (_, _) => { _matchCase = !_matchCase; UpdateToggleAppearance(_btnMatchCase, _matchCase); RunIncrementalSearch(); };

        _btnWholeWord = CreateToggleButton("W", "Whole Word");
        _btnWholeWord.Click += (_, _) => { _wholeWord = !_wholeWord; UpdateToggleAppearance(_btnWholeWord, _wholeWord); RunIncrementalSearch(); };

        _btnRegex = CreateToggleButton(".*", "Use Regular Expression");
        _btnRegex.Click += (_, _) => { _useRegex = !_useRegex; UpdateToggleAppearance(_btnRegex, _useRegex); RunIncrementalSearch(); };

        // ── Action buttons ────────────────────────────────────────────
        _btnFindPrev = CreateIconButton("\u25C0", "Find Previous");
        _btnFindPrev.Click += (_, _) => FindPrevious();

        _btnFindNext = CreateIconButton("\u25B6", "Find Next");
        _btnFindNext.Click += (_, _) => FindNext();

        _btnCount = CreateIconButton("#", "Count Matches");
        _btnCount.Click += (_, _) => CountMatches();

        _btnMarkAll = CreateTextButton("Mark All", "Highlight All Matches");
        _btnMarkAll.Click += (_, _) => MarkAll();

        _btnFindAll = CreateTextButton("Find All", "Find All in Current Document");
        _btnFindAll.Click += (_, _) => FindAll();

        _btnFindAllTabs = CreateTextButton("Find in Tabs", "Find All in All Open Tabs");
        _btnFindAllTabs.Click += (_, _) => FindAllInTabs();

        // ── Status ────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(4, 5, 2, 0),
        };

        // ── Close button ──────────────────────────────────────────────
        _btnClose = CreateIconButton("\u2715", "Close (Esc)");
        _btnClose.Click += (_, _) => ClosePanel();

        // ── Expand/collapse replace ───────────────────────────────────
        _btnExpandReplace = CreateIconButton("\u25BC", "Toggle Replace");
        _btnExpandReplace.Click += (_, _) => ToggleReplace();

        // ── Replace row ───────────────────────────────────────────────
        _replaceBox = new TextBox
        {
            Width = 220,
            Height = 26,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
        };
        _replaceBox.KeyDown += OnReplaceBoxKeyDown;

        _btnReplace = CreateTextButton("Replace", "Replace Current Match");
        _btnReplace.Click += (_, _) => ReplaceCurrent();

        _btnReplaceAll = CreateTextButton("All", "Replace All Matches");
        _btnReplaceAll.Click += (_, _) => ReplaceAll();

        _replaceRow = new Panel
        {
            Dock = DockStyle.None,
            Height = ReplaceRowHeight,
            Visible = false,
        };

        LayoutControls();
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Sets the editor control and text buffer that this panel searches within.
    /// </summary>
    public void Attach(EditorControl editor, PieceTable buffer)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

        // Cancel any in-flight search that references the old buffer.
        _searchCts?.Cancel();
    }

    /// <summary>
    /// The theme used for rendering panel colours.
    /// </summary>
    public ITheme? Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            ApplyTheme();
        }
    }

    /// <summary>
    /// Updates button text for localization.
    /// </summary>
    public void SetButtonTexts(string markAll, string findAll, string findInTabs,
        string replace, string replaceAll)
    {
        _btnMarkAll.Text = markAll;
        _btnFindAll.Text = findAll;
        _btnFindAllTabs.Text = findInTabs;
        _btnReplace.Text = replace;
        _btnReplaceAll.Text = replaceAll;
    }

    /// <summary>
    /// Sets the search text and optionally focuses the search box.
    /// </summary>
    public void SetSearchText(string text, bool focus = true)
    {
        _searchBox.Text = text;
        if (focus)
        {
            _searchBox.Focus();
            _searchBox.SelectAll();
        }
    }

    /// <summary>
    /// Whether the replace row is expanded.
    /// </summary>
    public bool ReplaceVisible
    {
        get => _replaceVisible;
        set
        {
            _replaceVisible = value;
            _replaceRow.Visible = value;
            Height = value
                ? FindRowHeight + 30 + ReplaceRowHeight + PanelPadding * 2
                : FindRowHeight + 30 + PanelPadding * 2;
            _btnExpandReplace.Text = value ? "\u25B2" : "\u25BC";
            PositionPanel();
        }
    }

    // ── Search operations ─────────────────────────────────────────────

    public void FindNext()
    {
        if (_buffer is null || string.IsNullOrEmpty(_searchBox.Text)) return;

        AddToHistory(_searchBox.Text);
        SearchOptions options = BuildSearchOptions(searchUp: false);
        long startOffset = GetSearchStartOffset(forward: true);

        _statusLabel.Text = "Searching...";
        FindNextRequested?.Invoke(this, new FindNextRequestEventArgs(options, startOffset));
    }

    public void FindPrevious()
    {
        if (_buffer is null || string.IsNullOrEmpty(_searchBox.Text)) return;

        AddToHistory(_searchBox.Text);
        SearchOptions options = BuildSearchOptions(searchUp: true);
        long startOffset = GetSearchStartOffset(forward: false);

        _statusLabel.Text = "Searching...";
        FindNextRequested?.Invoke(this, new FindNextRequestEventArgs(options, startOffset));
    }

    /// <summary>
    /// Called by MainForm after a background Find Next/Previous completes.
    /// </summary>
    public void DeliverFindNextResult(SearchResult? result)
    {
        if (result is not null)
        {
            _currentMatchIndex = FindMatchIndex(result.Offset);
            UpdateStatusLabel();
            NavigateToMatch?.Invoke(this, result);
        }
        else
        {
            _statusLabel.Text = "No matches";
            _currentMatchIndex = -1;
        }
    }

    public void CountMatches()
    {
        if (_buffer is null || string.IsNullOrEmpty(_searchBox.Text))
        {
            _statusLabel.Text = string.Empty;
            return;
        }

        SearchOptions options = BuildSearchOptions(searchUp: false);
        int count = _searchEngine.CountMatches(_buffer, options);
        _statusLabel.Text = $"{count} match{(count == 1 ? "" : "es")}";
    }

    public async void MarkAll()
    {
        if (_buffer is null || string.IsNullOrEmpty(_searchBox.Text)) return;

        AddToHistory(_searchBox.Text);
        SearchOptions options = BuildSearchOptions(searchUp: false);

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _btnMarkAll.Enabled = false;
        _statusLabel.Text = "Searching...";

        try
        {
            _currentMatches = await _searchEngine.FindAllAsync(_buffer, options,
                cancellationToken: token);
            UpdateStatusLabel();
            MatchesHighlighted?.Invoke(this, _currentMatches.Count > 0 ? BuildHighlightRegex() : null);
        }
        catch (OperationCanceledException)
        {
            // Cancelled — ignore.
        }
        catch (ObjectDisposedException)
        {
            // Buffer was disposed during incremental loading — treat as cancel.
        }
        finally
        {
            _btnMarkAll.Enabled = true;
        }
    }

    public void FindAll()
    {
        if (_buffer is null || string.IsNullOrEmpty(_searchBox.Text)) return;

        AddToHistory(_searchBox.Text);
        SearchOptions options = BuildSearchOptions(searchUp: false);

        // Cancel any in-flight incremental/mark search so it doesn't
        // interfere while MainForm runs the real search with progress.
        _searchCts?.Cancel();

        _statusLabel.Text = "Searching...";

        // Delegate the actual async search to MainForm (which shows a progress overlay).
        FindAllRequested?.Invoke(this, options);
    }

    /// <summary>
    /// Called by MainForm after the search completes to deliver results back
    /// for match highlighting and status label update.
    /// </summary>
    public void SetFindAllResults(List<SearchResult> results)
    {
        _currentMatches = results;
        _currentMatchIndex = _currentMatches.Count > 0 ? 0 : -1;
        UpdateStatusLabel();
        MatchesHighlighted?.Invoke(this, _currentMatches.Count > 0 ? BuildHighlightRegex() : null);
    }

    public void FindAllInTabs()
    {
        if (string.IsNullOrEmpty(_searchBox.Text)) return;

        AddToHistory(_searchBox.Text);
        SearchOptions options = BuildSearchOptions(searchUp: false);
        FindAllInTabsRequested?.Invoke(this, options);
    }

    /// <summary>The current search pattern text.</summary>
    public string SearchPattern => _searchBox.Text;

    public void ReplaceCurrent()
    {
        if (_buffer is null || _currentMatches.Count == 0 || _currentMatchIndex < 0)
            return;

        if (_currentMatchIndex >= _currentMatches.Count) return;

        SearchResult match = _currentMatches[_currentMatchIndex];
        SearchOptions options = BuildSearchOptions(searchUp: false);
        _searchEngine.Replace(_buffer, match, _replaceBox.Text, options);

        RunIncrementalSearch();
        FindNext();
    }

    public void ReplaceAll()
    {
        if (_buffer is null || string.IsNullOrEmpty(_searchBox.Text)) return;

        SearchOptions options = BuildSearchOptions(searchUp: false);
        int count = _searchEngine.ReplaceAll(_buffer, _replaceBox.Text, options);
        _currentMatches.Clear();
        _currentMatchIndex = -1;
        _statusLabel.Text = $"Replaced {count} occurrence{(count == 1 ? "" : "s")}";
    }

    public void ClosePanel()
    {
        _searchCts?.Cancel();
        Visible = false;
        _currentMatches.Clear();
        _currentMatchIndex = -1;
        PanelClosed?.Invoke(this, EventArgs.Empty);
    }

    // ── Keyboard handling ─────────────────────────────────────────────

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter when e.Shift:
                e.SuppressKeyPress = true;
                FindPrevious();
                break;

            case Keys.Enter:
                e.SuppressKeyPress = true;
                FindNext();
                break;

            case Keys.Escape:
                e.SuppressKeyPress = true;
                ClosePanel();
                break;
        }
    }

    private void OnReplaceBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                e.SuppressKeyPress = true;
                ReplaceCurrent();
                break;

            case Keys.Escape:
                e.SuppressKeyPress = true;
                ClosePanel();
                break;
        }
    }

    // ── Incremental search ────────────────────────────────────────────

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        RunIncrementalSearch();
    }

    private async void RunIncrementalSearch()
    {
        if (_buffer is null || string.IsNullOrEmpty(_searchBox.Text))
        {
            _currentMatches.Clear();
            _currentMatchIndex = -1;
            _statusLabel.Text = string.Empty;
            MatchesHighlighted?.Invoke(this, (Regex?)null);
            return;
        }

        SearchOptions options = BuildSearchOptions(searchUp: false);

        // Cancel any previous async search.
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            _currentMatches = await _searchEngine.FindAllAsync(_buffer, options,
                cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return; // Cancelled by a newer search.
        }
        catch (ObjectDisposedException)
        {
            return; // Buffer was disposed during incremental loading.
        }
        catch (ArgumentException)
        {
            _currentMatches.Clear();
            _statusLabel.Text = "Invalid pattern";
            return;
        }

        // If cancelled while running, discard partial results.
        if (token.IsCancellationRequested)
            return;

        _currentMatchIndex = _currentMatches.Count > 0 ? 0 : -1;
        UpdateStatusLabel();
        MatchesHighlighted?.Invoke(this, _currentMatches.Count > 0 ? BuildHighlightRegex() : null);

        if (_currentMatches.Count > 0)
            NavigateToMatch?.Invoke(this, _currentMatches[0]);
    }

    // ── History ───────────────────────────────────────────────────────

    private void AddToHistory(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _searchHistory.Remove(text);
        _searchHistory.Insert(0, text);

        if (_searchHistory.Count > MaxHistoryItems)
            _searchHistory.RemoveAt(_searchHistory.Count - 1);

        _searchBox.BeginUpdate();
        _searchBox.Items.Clear();
        foreach (string item in _searchHistory)
            _searchBox.Items.Add(item);
        _searchBox.EndUpdate();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a compiled <see cref="Regex"/> for highlighting matches on visible lines.
    /// Returns <see langword="null"/> if the pattern is empty or invalid.
    /// </summary>
    private Regex? BuildHighlightRegex()
    {
        if (string.IsNullOrEmpty(_searchBox.Text)) return null;
        try
        {
            return SearchEngine.BuildPattern(BuildSearchOptions(searchUp: false));
        }
        catch (ArgumentException)
        {
            return null; // Invalid regex pattern while typing.
        }
    }

    private SearchOptions BuildSearchOptions(bool searchUp)
    {
        return new SearchOptions
        {
            Pattern = _searchBox.Text,
            MatchCase = _matchCase,
            WholeWord = _wholeWord,
            UseRegex = _useRegex,
            SearchUp = searchUp,
            WrapAround = true,
            Scope = SearchScope.CurrentDocument,
        };
    }

    private long GetSearchStartOffset(bool forward)
    {
        if (_currentMatches.Count == 0 || _currentMatchIndex < 0)
            return 0;

        if (_currentMatchIndex >= _currentMatches.Count)
            return 0;

        SearchResult current = _currentMatches[_currentMatchIndex];
        return forward ? current.Offset + current.Length : current.Offset;
    }

    private int FindMatchIndex(long offset)
    {
        for (int i = 0; i < _currentMatches.Count; i++)
        {
            if (_currentMatches[i].Offset == offset)
                return i;
        }
        return -1;
    }

    private void UpdateStatusLabel()
    {
        if (_currentMatches.Count == 0)
        {
            _statusLabel.Text = "No matches";
        }
        else if (_currentMatchIndex >= 0)
        {
            bool capped = _currentMatches.Count >= Core.Search.SearchEngine.MaxResults;
            string countText = capped
                ? $"{Core.Search.SearchEngine.MaxResults:N0}+"
                : _currentMatches.Count.ToString();
            _statusLabel.Text = $"{_currentMatchIndex + 1} of {countText}";
        }
        else
        {
            bool capped = _currentMatches.Count >= Core.Search.SearchEngine.MaxResults;
            string countText = capped
                ? $"{Core.Search.SearchEngine.MaxResults:N0}+"
                : _currentMatches.Count.ToString();
            _statusLabel.Text = $"{countText} match{(_currentMatches.Count == 1 ? "" : "es")}";
        }
    }

    private void ToggleReplace()
    {
        ReplaceVisible = !ReplaceVisible;
    }

    // ── Layout ────────────────────────────────────────────────────────

    private void PositionPanel()
    {
        // Called on parent resize too — keeps us right-aligned.
        if (Parent is not null)
        {
            int x = Parent.ClientSize.Width - PanelWidth - 16;
            if (x < 0) x = 0;
            Location = new Point(x, 0);
            Width = Math.Min(PanelWidth, Parent.ClientSize.Width);
        }
    }

    private void LayoutControls()
    {
        // Override dock so we can position ourselves at the right.
        Dock = DockStyle.None;
        Width = PanelWidth;
        Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // ── Find row ─────────────────────────────────────────────────
        var findRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = FindRowHeight,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(PanelPadding, 4, PanelPadding, 0),
        };

        findRow.Controls.AddRange([
            _btnExpandReplace,
            _searchBox,
            _btnMatchCase,
            _btnWholeWord,
            _btnRegex,
            _btnFindPrev,
            _btnFindNext,
            _btnClose,
        ]);

        // ── Second row: action buttons + status ──────────────────────
        var actionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(PanelPadding + 26, 0, PanelPadding, 0),
        };

        actionsRow.Controls.AddRange([
            _btnCount,
            _btnMarkAll,
            _btnFindAll,
            _btnFindAllTabs,
            _statusLabel,
        ]);

        // ── Replace row ──────────────────────────────────────────────
        var replaceFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(PanelPadding + 26, 4, PanelPadding, 0),
        };

        replaceFlow.Controls.AddRange([_replaceBox, _btnReplace, _btnReplaceAll]);
        _replaceRow.Controls.Add(replaceFlow);
        _replaceRow.Dock = DockStyle.Top;

        Controls.Add(_replaceRow);
        Controls.Add(actionsRow);
        Controls.Add(findRow);

        // Position ourselves when parent resizes.
        ParentChanged += (_, _) =>
        {
            PositionPanel();
            if (Parent is not null)
            {
                Parent.Resize -= OnParentResize;
                Parent.Resize += OnParentResize;
            }
        };
    }

    private void OnParentResize(object? sender, EventArgs e) => PositionPanel();

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) PositionPanel();
    }

    // ── Painting ──────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw rounded border + shadow.
        Color borderColor = _theme is not null
            ? Lighten(_theme.FindPanelBackground, 40)
            : Color.FromArgb(80, 80, 80);
        Color shadowColor = Color.FromArgb(40, 0, 0, 0);

        using var shadowPen = new Pen(shadowColor, 1);
        g.DrawRectangle(shadowPen, 0, 0, Width - 1, Height - 1);

        using var borderPen = new Pen(borderColor, 1);
        g.DrawRectangle(borderPen, 1, 0, Width - 3, Height - 2);
    }

    // ── Theme ─────────────────────────────────────────────────────────

    private void ApplyTheme()
    {
        if (_theme is null) return;

        BackColor = _theme.FindPanelBackground;
        ForeColor = _theme.FindPanelForeground;

        // Input boxes.
        Color inputBg = Lighten(_theme.FindPanelBackground, 15);
        _searchBox.BackColor = inputBg;
        _searchBox.ForeColor = _theme.FindPanelForeground;
        _replaceBox.BackColor = inputBg;
        _replaceBox.ForeColor = _theme.FindPanelForeground;
        _statusLabel.ForeColor = Color.FromArgb(160, _theme.FindPanelForeground);

        // Button styling.
        Color btnBg = Lighten(_theme.FindPanelBackground, 8);
        Color btnFg = _theme.FindPanelForeground;
        Color btnBorder = Lighten(_theme.FindPanelBackground, 25);
        Color hoverBg = Lighten(btnBg, 15);
        Color pressBg = Lighten(btnBg, 25);

        foreach (Control c in GetAllButtons(this))
        {
            if (c is Button btn)
            {
                btn.BackColor = btnBg;
                btn.ForeColor = btnFg;
                btn.FlatAppearance.BorderColor = btnBorder;
                btn.FlatAppearance.MouseOverBackColor = hoverBg;
                btn.FlatAppearance.MouseDownBackColor = pressBg;
            }
        }

        // Re-apply toggle states.
        UpdateToggleAppearance(_btnMatchCase, _matchCase);
        UpdateToggleAppearance(_btnWholeWord, _wholeWord);
        UpdateToggleAppearance(_btnRegex, _useRegex);

        Invalidate(true);
    }

    private static Color Lighten(Color c, int amount)
    {
        return Color.FromArgb(c.A,
            Math.Min(255, c.R + amount),
            Math.Min(255, c.G + amount),
            Math.Min(255, c.B + amount));
    }

    private static IEnumerable<Control> GetAllButtons(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is Button) yield return c;
            foreach (Control child in GetAllButtons(c))
                yield return child;
        }
    }

    // ── Control factory helpers ───────────────────────────────────────

    private static Button CreateToggleButton(string text, string tooltip)
    {
        var btn = new Button
        {
            Text = text,
            Width = 32,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(1, 1, 1, 1),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 1;

        var tt = new ToolTip { InitialDelay = 400 };
        tt.SetToolTip(btn, tooltip);

        return btn;
    }

    private static void UpdateToggleAppearance(Button btn, bool active)
    {
        if (active)
        {
            btn.BackColor = Color.FromArgb(40, 80, 140);
            btn.FlatAppearance.BorderColor = Color.FromArgb(70, 120, 190);
            btn.ForeColor = Color.FromArgb(230, 230, 230);
        }
        else
        {
            btn.BackColor = Color.FromArgb(55, 55, 60);
            btn.FlatAppearance.BorderColor = Color.FromArgb(75, 75, 80);
            btn.ForeColor = Color.FromArgb(170, 170, 170);
        }
    }

    private static Button CreateIconButton(string text, string tooltip)
    {
        var btn = new Button
        {
            Text = text,
            Width = 32,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(1, 1, 1, 1),
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;

        var tt = new ToolTip { InitialDelay = 400 };
        tt.SetToolTip(btn, tooltip);

        return btn;
    }

    private static Button CreateTextButton(string text, string tooltip)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(2, 1, 2, 1),
            Padding = new Padding(8, 2, 8, 2),
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 1;

        var tt = new ToolTip { InitialDelay = 400 };
        tt.SetToolTip(btn, tooltip);

        return btn;
    }

    // ── Disposal ──────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _debounceTimer.Dispose();
            if (Parent is not null)
                Parent.Resize -= OnParentResize;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Event arguments for a Find Next/Previous request that should run
/// on a background thread with progress overlay.
/// </summary>
public sealed class FindNextRequestEventArgs : EventArgs
{
    public SearchOptions Options { get; }
    public long StartOffset { get; }

    public FindNextRequestEventArgs(SearchOptions options, long startOffset)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        StartOffset = startOffset;
    }
}
