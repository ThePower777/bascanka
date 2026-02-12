using Bascanka.Core.Buffer;

namespace Bascanka.Core.Commands;

/// <summary>
/// Inserts text into a <see cref="PieceTable"/> at a specified offset.
/// Consecutive single-character inserts at adjacent positions within a short
/// time window are automatically merged into a single undo step.
/// </summary>
public sealed class InsertCommand : ICommand
{
    /// <summary>
    /// Maximum elapsed time between two inserts that still allows merging.
    /// </summary>
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    private readonly PieceTable _pieceTable;
    private long _offset;
    private string _text;
    private DateTime _timestamp;

    /// <summary>
    /// Creates a new insert command.
    /// </summary>
    /// <param name="pieceTable">The piece table to modify.</param>
    /// <param name="offset">Character offset at which the text will be inserted.</param>
    /// <param name="text">The text to insert.</param>
    public InsertCommand(PieceTable pieceTable, long offset, string text)
    {
        _pieceTable = pieceTable ?? throw new ArgumentNullException(nameof(pieceTable));
        _offset = offset;
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _timestamp = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public string Description => "Insert text";

    /// <summary>
    /// The character offset at which the insertion begins.
    /// </summary>
    public long Offset => _offset;

    /// <summary>
    /// The text that was (or will be) inserted.
    /// </summary>
    public string Text => _text;

    /// <inheritdoc />
    public void Execute()
    {
        _pieceTable.Insert(_offset, _text);
        _timestamp = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public void Undo()
    {
        _pieceTable.Delete(_offset, _text.Length);
    }

    /// <inheritdoc />
    public bool CanMergeWith(ICommand other)
    {
        if (other is not InsertCommand insert)
            return false;

        // The new insert must be immediately after the end of this one.
        if (insert._offset != _offset + _text.Length)
            return false;

        // Must be within the merge time window.
        if (insert._timestamp - _timestamp > MergeWindow)
            return false;

        return true;
    }

    /// <inheritdoc />
    public void MergeWith(ICommand other)
    {
        if (other is not InsertCommand insert)
            throw new ArgumentException("Cannot merge with a non-InsertCommand.", nameof(other));

        _text += insert._text;
        _timestamp = insert._timestamp;
    }
}
