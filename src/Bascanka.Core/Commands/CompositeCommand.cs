namespace Bascanka.Core.Commands;

/// <summary>
/// Groups multiple <see cref="ICommand"/> instances into a single undoable operation.
/// All child commands are executed in order and undone in reverse order.
/// </summary>
public sealed class CompositeCommand : ICommand
{
    private readonly List<ICommand> _commands;
    private readonly string _description;

    /// <summary>
    /// Creates a new composite command.
    /// </summary>
    /// <param name="description">
    /// A human-readable description for this composite operation.
    /// </param>
    /// <param name="commands">The child commands to group together.</param>
    public CompositeCommand(string description, IEnumerable<ICommand> commands)
    {
        _description = description ?? throw new ArgumentNullException(nameof(description));
        _commands = new List<ICommand>(commands ?? throw new ArgumentNullException(nameof(commands)));
    }

    /// <summary>
    /// Creates a new composite command.
    /// </summary>
    /// <param name="description">
    /// A human-readable description for this composite operation.
    /// </param>
    /// <param name="commands">The child commands to group together.</param>
    public CompositeCommand(string description, params ICommand[] commands)
        : this(description, (IEnumerable<ICommand>)commands)
    {
    }

    /// <inheritdoc />
    public string Description => _description;

    /// <summary>
    /// The child commands contained in this composite, in execution order.
    /// </summary>
    public IReadOnlyList<ICommand> Commands => _commands;

    /// <inheritdoc />
    public void Execute()
    {
        for (int i = 0; i < _commands.Count; i++)
        {
            _commands[i].Execute();
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Composite commands are not automatically merged; they represent
    /// intentionally grouped operations.
    /// </remarks>
    public bool CanMergeWith(ICommand other) => false;

    /// <inheritdoc />
    public void MergeWith(ICommand other)
    {
        throw new NotSupportedException("CompositeCommand does not support merging.");
    }
}
