namespace Bascanka.Plugins.Api;

/// <summary>
/// Provides low-level access to the text content of a document buffer.
/// All offsets are zero-based character positions; line indices are
/// zero-based as well.
/// </summary>
public interface IBufferApi
{
    /// <summary>
    /// Reads a substring from the buffer.
    /// </summary>
    /// <param name="offset">The zero-based character offset to start reading from.</param>
    /// <param name="length">The number of characters to read.</param>
    /// <returns>The requested text span.</returns>
    string GetText(long offset, long length);

    /// <summary>
    /// Inserts text at the specified offset. Existing text at and after the
    /// offset is shifted to the right.
    /// </summary>
    /// <param name="offset">The zero-based character offset at which to insert.</param>
    /// <param name="text">The text to insert.</param>
    void Insert(long offset, string text);

    /// <summary>
    /// Deletes a range of characters from the buffer.
    /// </summary>
    /// <param name="offset">The zero-based start offset of the range to delete.</param>
    /// <param name="length">The number of characters to delete.</param>
    void Delete(long offset, long length);

    /// <summary>
    /// Replaces a range of characters with new text. Equivalent to a delete
    /// followed by an insert, but executed atomically.
    /// </summary>
    /// <param name="offset">The zero-based start offset of the range to replace.</param>
    /// <param name="length">The number of characters to remove.</param>
    /// <param name="newText">The replacement text.</param>
    void Replace(long offset, long length, string newText);

    /// <summary>Gets the total number of characters in the buffer.</summary>
    long Length { get; }

    /// <summary>Gets the total number of lines in the buffer.</summary>
    long LineCount { get; }

    /// <summary>
    /// Returns the full text of a single line, excluding the line terminator.
    /// </summary>
    /// <param name="lineIndex">The zero-based line index.</param>
    /// <returns>The line text.</returns>
    string GetLine(long lineIndex);

    /// <summary>
    /// Converts a character offset to a (line, column) pair.
    /// Both line and column values are zero-based.
    /// </summary>
    /// <param name="offset">The zero-based character offset.</param>
    /// <returns>A tuple of (Line, Column).</returns>
    (long Line, long Column) OffsetToLineColumn(long offset);

    /// <summary>
    /// Converts a (line, column) pair to a character offset.
    /// Both line and column values are zero-based.
    /// </summary>
    /// <param name="line">The zero-based line index.</param>
    /// <param name="column">The zero-based column index.</param>
    /// <returns>The character offset.</returns>
    long LineColumnToOffset(long line, long column);
}
