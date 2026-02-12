namespace Bascanka.Core.Commands;

/// <summary>
/// Manages undo/redo history for a text document.
/// Supports command merging, save-point tracking, and configurable depth limits.
/// </summary>
public sealed class CommandHistory
{
    private readonly LinkedList<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    private int _maxUndoLevels = 10_000;

    /// <summary>
    /// The command that was on top of the undo stack when <see cref="SetSavePoint"/>
    /// was last called, or <see langword="null"/> if the save point is the empty state.
    /// </summary>
    private ICommand? _savePointCommand;

    /// <summary>
    /// Whether a save point has ever been set. Distinguishes "saved at empty state"
    /// from "never saved".
    /// </summary>
    private bool _savePointSet;

    /// <summary>
    /// Raised whenever the undo or redo stacks change (after execute, undo, redo, or clear).
    /// </summary>
    public event EventHandler? UndoStackChanged;

    /// <summary>
    /// Raised whenever the <see cref="IsDirty"/> state changes.
    /// </summary>
    public event EventHandler? SavePointChanged;

    /// <summary>
    /// Gets or sets the maximum number of undo levels retained.
    /// When the limit is exceeded the oldest commands are silently dropped.
    /// The default value is 10,000.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the value is less than 1.
    /// </exception>
    public int MaxUndoLevels
    {
        get => _maxUndoLevels;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "MaxUndoLevels must be at least 1.");

            _maxUndoLevels = value;
            TrimUndoStack();
        }
    }

    /// <summary>
    /// <see langword="true"/> if there is at least one command that can be undone.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// <see langword="true"/> if there is at least one command that can be redone.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// <see langword="true"/> if the current document state differs from the last save point.
    /// If no save point has been set, the document is considered dirty as soon as
    /// any command has been executed.
    /// </summary>
    public bool IsDirty
    {
        get
        {
            if (!_savePointSet)
                return _undoStack.Count > 0;

            ICommand? currentTop = _undoStack.Last?.Value;
            return !ReferenceEquals(currentTop, _savePointCommand);
        }
    }

    /// <summary>
    /// Returns a human-readable description of the next command that would be undone,
    /// or <see langword="null"/> if the undo stack is empty.
    /// </summary>
    public string? UndoDescription => _undoStack.Last?.Value.Description;

    /// <summary>
    /// Returns a human-readable description of the next command that would be redone,
    /// or <see langword="null"/> if the redo stack is empty.
    /// </summary>
    public string? RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    /// <summary>
    /// Executes a command and pushes it onto the undo stack.
    /// If the command can be merged with the most recent undo entry, it is merged
    /// instead of creating a new entry. Executing a new command always clears the
    /// redo stack.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    public void Execute(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool wasDirty = IsDirty;

        command.Execute();

        // Clear the redo stack -- a new action invalidates the redo history.
        _redoStack.Clear();

        // Try to merge with the most recent command.
        if (_undoStack.Last is not null && _undoStack.Last.Value.CanMergeWith(command))
        {
            _undoStack.Last.Value.MergeWith(command);
        }
        else
        {
            _undoStack.AddLast(command);
            TrimUndoStack();
        }

        OnUndoStackChanged();
        NotifySavePointIfChanged(wasDirty);
    }

    /// <summary>
    /// Undoes the most recent command on the undo stack and moves it to the redo stack.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the undo stack is empty.
    /// </exception>
    public void Undo()
    {
        if (_undoStack.Last is null)
            throw new InvalidOperationException("Nothing to undo.");

        bool wasDirty = IsDirty;

        ICommand command = _undoStack.Last.Value;
        _undoStack.RemoveLast();

        command.Undo();

        _redoStack.Push(command);

        OnUndoStackChanged();
        NotifySavePointIfChanged(wasDirty);
    }

    /// <summary>
    /// Re-executes the most recent command on the redo stack and moves it back
    /// to the undo stack.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the redo stack is empty.
    /// </exception>
    public void Redo()
    {
        if (_redoStack.Count == 0)
            throw new InvalidOperationException("Nothing to redo.");

        bool wasDirty = IsDirty;

        ICommand command = _redoStack.Pop();

        command.Execute();

        _undoStack.AddLast(command);
        TrimUndoStack();

        OnUndoStackChanged();
        NotifySavePointIfChanged(wasDirty);
    }

    /// <summary>
    /// Marks the current document state as the "saved" state.
    /// <see cref="IsDirty"/> will return <see langword="false"/> until the
    /// undo/redo position diverges from this point.
    /// </summary>
    public void SetSavePoint()
    {
        bool wasDirty = IsDirty;

        _savePointCommand = _undoStack.Last?.Value;
        _savePointSet = true;

        NotifySavePointIfChanged(wasDirty);
    }

    /// <summary>
    /// Clears both the undo and redo stacks and resets the save-point state.
    /// </summary>
    public void Clear()
    {
        bool wasDirty = IsDirty;

        _undoStack.Clear();
        _redoStack.Clear();
        _savePointCommand = null;
        _savePointSet = false;

        OnUndoStackChanged();
        NotifySavePointIfChanged(wasDirty);
    }

    /// <summary>
    /// Removes the oldest commands from the undo stack until it is within
    /// <see cref="MaxUndoLevels"/>.
    /// </summary>
    private void TrimUndoStack()
    {
        while (_undoStack.Count > _maxUndoLevels)
        {
            ICommand removed = _undoStack.First!.Value;
            _undoStack.RemoveFirst();

            // If the trimmed command was the save-point, the save point is now
            // unreachable -- the document is permanently dirty until the next save.
            if (_savePointSet && ReferenceEquals(removed, _savePointCommand))
            {
                _savePointCommand = null;
                // We keep _savePointSet = true so that IsDirty doesn't fall back
                // to the "never saved" heuristic. The save point is simply gone.
            }
        }
    }

    private void OnUndoStackChanged()
    {
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifySavePointIfChanged(bool wasDirty)
    {
        if (IsDirty != wasDirty)
        {
            SavePointChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
