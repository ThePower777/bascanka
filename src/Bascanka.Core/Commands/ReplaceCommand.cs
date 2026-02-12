using Bascanka.Core.Buffer;

namespace Bascanka.Core.Commands;

/// <summary>
/// Atomically replaces a range of text with new text.
/// Internally composed of a <see cref="DeleteCommand"/> followed by an
/// <see cref="InsertCommand"/>, but presented as a single undo step.
/// </summary>
public sealed class ReplaceCommand : ICommand
{
    private readonly DeleteCommand _delete;
    private readonly InsertCommand _insert;

    /// <summary>
    /// Creates a new replace command.
    /// </summary>
    /// <param name="pieceTable">The piece table to modify.</param>
    /// <param name="offset">Character offset at which the replacement begins.</param>
    /// <param name="length">Number of characters to remove.</param>
    /// <param name="newText">The replacement text to insert.</param>
    public ReplaceCommand(PieceTable pieceTable, long offset, long length, string newText)
    {
        ArgumentNullException.ThrowIfNull(pieceTable);
        ArgumentNullException.ThrowIfNull(newText);

        _delete = new DeleteCommand(pieceTable, offset, length);
        _insert = new InsertCommand(pieceTable, offset, newText);
    }

    /// <inheritdoc />
    public string Description => "Replace text";

    /// <inheritdoc />
    public void Execute()
    {
        _delete.Execute();
        _insert.Execute();
    }

    /// <inheritdoc />
    public void Undo()
    {
        // Reverse order: undo the insert first, then undo the delete.
        _insert.Undo();
        _delete.Undo();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Replace commands are not automatically merged because they typically
    /// represent discrete, deliberate operations (find-and-replace, paste-over, etc.).
    /// </remarks>
    public bool CanMergeWith(ICommand other) => false;

    /// <inheritdoc />
    public void MergeWith(ICommand other)
    {
        throw new NotSupportedException("ReplaceCommand does not support merging.");
    }
}
