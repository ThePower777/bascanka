using Bascanka.Core.Buffer;

namespace Bascanka.Core.Commands;

/// <summary>
/// Deletes a range of text from a <see cref="PieceTable"/>.
/// The deleted text is captured on the first execution so that
/// <see cref="Undo"/> can restore it.
/// </summary>
public sealed class DeleteCommand : ICommand
{
    /// <summary>
    /// Maximum elapsed time between two deletes that still allows merging.
    /// </summary>
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    private readonly PieceTable _pieceTable;
    private long _offset;
    private long _length;
    private string? _deletedText;
    private DateTime _timestamp;

    /// <summary>
    /// Creates a new delete command.
    /// </summary>
    /// <param name="pieceTable">The piece table to modify.</param>
    /// <param name="offset">Character offset at which the deletion starts.</param>
    /// <param name="length">Number of characters to delete.</param>
    public DeleteCommand(PieceTable pieceTable, long offset, long length)
    {
        _pieceTable = pieceTable ?? throw new ArgumentNullException(nameof(pieceTable));
        _offset = offset;
        _length = length;
        _timestamp = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public string Description => "Delete text";

    /// <summary>
    /// The character offset at which the deletion starts.
    /// </summary>
    public long Offset => _offset;

    /// <summary>
    /// The number of characters that were (or will be) deleted.
    /// </summary>
    public long Length => _length;

    /// <summary>
    /// The text that was deleted. Available only after the first <see cref="Execute"/> call.
    /// </summary>
    public string? DeletedText => _deletedText;

    /// <inheritdoc />
    public void Execute()
    {
        // Capture the text being deleted so we can restore it on Undo.
        _deletedText ??= _pieceTable.GetText(_offset, _length);

        _pieceTable.Delete(_offset, _length);
        _timestamp = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public void Undo()
    {
        if (_deletedText is null)
            throw new InvalidOperationException("Cannot undo a delete that has never been executed.");

        _pieceTable.Insert(_offset, _deletedText);
    }

    /// <inheritdoc />
    public bool CanMergeWith(ICommand other)
    {
        if (other is not DeleteCommand delete)
            return false;

        // Must be within the merge time window.
        if (delete._timestamp - _timestamp > MergeWindow)
            return false;

        // Forward-delete: the new delete starts at the same offset (user presses Delete key).
        if (delete._offset == _offset)
            return true;

        // Backspace: the new delete ends where this one begins
        // (user presses Backspace, so offset decreases by one each time).
        if (delete._offset + delete._length == _offset)
            return true;

        return false;
    }

    /// <inheritdoc />
    public void MergeWith(ICommand other)
    {
        if (other is not DeleteCommand delete)
            throw new ArgumentException("Cannot merge with a non-DeleteCommand.", nameof(other));

        if (delete._offset == _offset)
        {
            // Forward-delete: append the newly deleted text.
            _deletedText += delete._deletedText;
            _length += delete._length;
        }
        else if (delete._offset + delete._length == _offset)
        {
            // Backspace: prepend the newly deleted text and move the offset back.
            _deletedText = delete._deletedText + _deletedText;
            _offset = delete._offset;
            _length += delete._length;
        }

        _timestamp = delete._timestamp;
    }
}
