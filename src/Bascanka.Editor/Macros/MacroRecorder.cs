namespace Bascanka.Editor.Macros;

/// <summary>
/// Records a sequence of <see cref="MacroAction"/> instances as the user
/// interacts with the editor.  The editor's input handler should call
/// <see cref="RecordAction"/> for every actionable event while
/// <see cref="IsRecording"/> is <see langword="true"/>.
/// </summary>
public sealed class MacroRecorder
{
    // ── Fields ──────────────────────────────────────────────────────────

    private readonly List<MacroAction> _actions = [];
    private bool _isRecording;

    // ── Events ──────────────────────────────────────────────────────────

    /// <summary>
    /// Raised whenever the recording state changes (started or stopped).
    /// </summary>
    public event EventHandler? RecordingStateChanged;

    /// <summary>
    /// Raised each time a new action is recorded, allowing UI elements
    /// (such as a status-bar indicator) to update in real time.
    /// </summary>
    public event EventHandler<MacroAction>? ActionRecorded;

    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>
    /// <see langword="true"/> while a recording session is in progress.
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// A read-only view of the actions captured so far in the current
    /// recording session.  Useful for displaying a live action list.
    /// Returns an empty list when not recording.
    /// </summary>
    public IReadOnlyList<MacroAction> CurrentActions => _actions.AsReadOnly();

    // ── Recording lifecycle ─────────────────────────────────────────────

    /// <summary>
    /// Begins a new recording session.  Any previously recorded actions
    /// are discarded.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a recording is already in progress.
    /// </exception>
    public void StartRecording()
    {
        if (_isRecording)
            throw new InvalidOperationException("A recording session is already in progress.");

        _actions.Clear();
        _isRecording = true;
        RecordingStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stops the current recording session and returns the captured
    /// sequence of actions as a new <see cref="Macro"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="Macro"/> containing every action recorded since the
    /// last call to <see cref="StartRecording"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no recording is in progress.
    /// </exception>
    public Macro StopRecording()
    {
        if (!_isRecording)
            throw new InvalidOperationException("No recording session is in progress.");

        _isRecording = false;

        var macro = new Macro
        {
            Name = $"Macro {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            Actions = new List<MacroAction>(_actions),
            Created = DateTime.Now,
        };

        _actions.Clear();
        RecordingStateChanged?.Invoke(this, EventArgs.Empty);

        return macro;
    }

    /// <summary>
    /// Records a single action.  This method is a no-op when
    /// <see cref="IsRecording"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="action">The action to record.</param>
    public void RecordAction(MacroAction action)
    {
        if (!_isRecording) return;
        ArgumentNullException.ThrowIfNull(action);

        // Merge consecutive TypeText actions into a single entry to keep
        // the action list compact.
        if (action.ActionType == MacroActionType.TypeText &&
            _actions.Count > 0 &&
            _actions[^1].ActionType == MacroActionType.TypeText)
        {
            MacroAction previous = _actions[^1];
            _actions[^1] = new MacroAction
            {
                ActionType = MacroActionType.TypeText,
                Text = previous.Text + action.Text,
            };
        }
        else
        {
            _actions.Add(action);
        }

        ActionRecorded?.Invoke(this, action);
    }

    /// <summary>
    /// Cancels the current recording session, discarding all captured actions.
    /// Does nothing if no recording is in progress.
    /// </summary>
    public void CancelRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;
        _actions.Clear();
        RecordingStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
