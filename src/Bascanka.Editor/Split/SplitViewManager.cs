using Bascanka.Core.Buffer;
using Bascanka.Editor.Controls;

namespace Bascanka.Editor.Split;

/// <summary>
/// Manages splitting an editor view into two independent panes that share
/// the same <see cref="PieceTable"/> document but maintain separate scroll
/// positions and caret locations.
/// <para>
/// Uses a <see cref="SplitContainer"/> to divide the available space either
/// horizontally or vertically.  Both panes receive the same document
/// reference, so all edits in one pane are immediately visible in the other.
/// An optional synchronised-scrolling mode keeps both views aligned.
/// </para>
/// </summary>
public class SplitViewManager : IDisposable
{
    // ── State ─────────────────────────────────────────────────────────
    private readonly Control _hostPanel;
    private SplitContainer? _splitContainer;
    private EditorControl? _primaryEditor;
    private EditorControl? _secondaryEditor;
    private Orientation _splitOrientation;
    private bool _syncScrolling;
    private bool _disposed;

    // ── Events ────────────────────────────────────────────────────────

    /// <summary>
    /// Raised after a split is created or removed, allowing the host to
    /// update menus or status indicators.
    /// </summary>
    public event EventHandler? SplitChanged;

    // ── Construction ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="SplitViewManager"/> that manages editor
    /// splitting within the given host panel.
    /// </summary>
    /// <param name="hostPanel">
    /// The container control (typically a <see cref="Panel"/>) that holds
    /// the editor.  When split, the host's contents are rearranged into a
    /// <see cref="SplitContainer"/>.
    /// </param>
    public SplitViewManager(Control hostPanel)
    {
        _hostPanel = hostPanel ?? throw new ArgumentNullException(nameof(hostPanel));
    }

    // ── Public properties ─────────────────────────────────────────────

    /// <summary>
    /// Whether the editor is currently split into two panes.
    /// </summary>
    public bool IsSplit => _splitContainer is not null;

    /// <summary>
    /// The primary (original) editor control.
    /// </summary>
    public EditorControl? PrimaryEditor => _primaryEditor;

    /// <summary>
    /// The secondary editor control (created during a split), or
    /// <see langword="null"/> if not split.
    /// </summary>
    public EditorControl? SecondaryEditor => _secondaryEditor;

    /// <summary>
    /// When <see langword="true"/>, scrolling one pane scrolls the other
    /// pane by the same amount.  Only meaningful when <see cref="IsSplit"/>
    /// is <see langword="true"/>.
    /// </summary>
    public bool SyncScrolling
    {
        get => _syncScrolling;
        set
        {
            _syncScrolling = value;
            if (_syncScrolling && IsSplit)
            {
                SynchronizeScroll(_primaryEditor!, _secondaryEditor!);
            }
        }
    }

    /// <summary>
    /// The orientation of the current split.  Only meaningful when
    /// <see cref="IsSplit"/> is <see langword="true"/>.
    /// </summary>
    public Orientation SplitOrientation => _splitOrientation;

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Sets the primary editor that will be managed by this split view.
    /// Must be called before <see cref="SplitHorizontal"/> or
    /// <see cref="SplitVertical"/>.
    /// </summary>
    public void SetPrimaryEditor(EditorControl editor)
    {
        _primaryEditor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    /// <summary>
    /// Splits the editor view horizontally (top/bottom panes).
    /// Both panes share the same <see cref="PieceTable"/> document.
    /// </summary>
    public void SplitHorizontal()
    {
        Split(Orientation.Horizontal);
    }

    /// <summary>
    /// Splits the editor view vertically (left/right panes).
    /// Both panes share the same <see cref="PieceTable"/> document.
    /// </summary>
    public void SplitVertical()
    {
        Split(Orientation.Vertical);
    }

    /// <summary>
    /// Removes the split and returns to a single-pane view.  The primary
    /// editor is kept; the secondary editor is disposed.
    /// </summary>
    public void RemoveSplit()
    {
        if (!IsSplit || _splitContainer is null || _primaryEditor is null)
            return;

        // Detach the secondary editor.
        if (_secondaryEditor is not null)
        {
            DetachScrollSync(_secondaryEditor);
            _splitContainer.Panel2.Controls.Remove(_secondaryEditor);
            _secondaryEditor.Dispose();
            _secondaryEditor = null;
        }

        // Remove the primary editor from the split container and put it
        // back into the host panel.
        _splitContainer.Panel1.Controls.Remove(_primaryEditor);
        _hostPanel.Controls.Remove(_splitContainer);

        _splitContainer.Dispose();
        _splitContainer = null;

        _primaryEditor.Dock = DockStyle.Fill;
        _hostPanel.Controls.Add(_primaryEditor);

        SplitChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Split implementation ──────────────────────────────────────────

    private void Split(Orientation orientation)
    {
        if (_primaryEditor is null)
            throw new InvalidOperationException(
                "SetPrimaryEditor must be called before splitting.");

        // If already split, remove existing split first.
        if (IsSplit)
            RemoveSplit();

        _splitOrientation = orientation;

        // Create the secondary editor sharing the same document.
        _secondaryEditor = CreateSecondaryEditor(_primaryEditor);

        // Build the split container.
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = orientation,
            SplitterWidth = 4,
            BorderStyle = BorderStyle.None,
        };

        // Move the primary editor from the host into panel 1.
        _hostPanel.Controls.Remove(_primaryEditor);
        _primaryEditor.Dock = DockStyle.Fill;
        _splitContainer.Panel1.Controls.Add(_primaryEditor);

        // Place the secondary editor into panel 2.
        _secondaryEditor.Dock = DockStyle.Fill;
        _splitContainer.Panel2.Controls.Add(_secondaryEditor);

        _hostPanel.Controls.Add(_splitContainer);

        // Set the splitter at 50%.
        _splitContainer.SplitterDistance = orientation == Orientation.Horizontal
            ? _hostPanel.Height / 2
            : _hostPanel.Width / 2;

        // Wire up scroll synchronisation.
        if (_syncScrolling)
        {
            AttachScrollSync(_primaryEditor, _secondaryEditor);
        }

        SplitChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates a new <see cref="EditorControl"/> that shares the same
    /// <see cref="PieceTable"/> document as the primary editor.
    /// </summary>
    private static EditorControl CreateSecondaryEditor(EditorControl primary)
    {
        var secondary = new EditorControl
        {
            Document = primary.Document,
            Font = primary.Font,
            BackColor = primary.BackColor,
            ForeColor = primary.ForeColor,
        };
        return secondary;
    }

    // ── Scroll synchronisation ────────────────────────────────────────

    private void AttachScrollSync(EditorControl primary, EditorControl secondary)
    {
        primary.CaretMoved += OnPrimaryCaretMoved;
        secondary.CaretMoved += OnSecondaryCaretMoved;
    }

    private void DetachScrollSync(EditorControl editor)
    {
        editor.CaretMoved -= OnPrimaryCaretMoved;
        editor.CaretMoved -= OnSecondaryCaretMoved;
    }

    private void OnPrimaryCaretMoved(object? sender, long offset)
    {
        if (!_syncScrolling || _secondaryEditor is null || _primaryEditor is null)
            return;

        SynchronizeScroll(_primaryEditor, _secondaryEditor);
    }

    private void OnSecondaryCaretMoved(object? sender, long offset)
    {
        if (!_syncScrolling || _primaryEditor is null || _secondaryEditor is null)
            return;

        SynchronizeScroll(_secondaryEditor, _primaryEditor);
    }

    /// <summary>
    /// Scrolls the <paramref name="target"/> editor to the same line as
    /// the <paramref name="source"/> editor.
    /// </summary>
    private static void SynchronizeScroll(EditorControl source, EditorControl target)
    {
        long sourceLine = source.CaretLine;
        target.ScrollToLine(sourceLine);
    }

    // ── IDisposable ───────────────────────────────────────────────────

    /// <summary>
    /// Cleans up the split container and secondary editor.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            if (IsSplit)
                RemoveSplit();
        }
    }
}
