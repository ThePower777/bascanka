namespace Bascanka.Core.Buffer;

/// <summary>
/// Identifies which buffer a <see cref="Piece"/> references.
/// </summary>
public enum BufferType : byte
{
    /// <summary>The immutable original-text buffer.</summary>
    Original = 0,

    /// <summary>The append-only buffer that accumulates inserted text.</summary>
    Add = 1,
}

/// <summary>
/// A descriptor that points into either the original or add buffer.
/// Together with the piece table tree these form the logical document.
/// </summary>
public readonly struct Piece : IEquatable<Piece>
{
    /// <summary>Which text buffer this piece references.</summary>
    public BufferType BufferType { get; }

    /// <summary>Start offset inside the referenced buffer (character index).</summary>
    public long Start { get; }

    /// <summary>Number of characters covered by this piece.</summary>
    public long Length { get; }

    /// <summary>
    /// Cached count of <c>'\n'</c> characters contained in the text region
    /// this piece describes. Kept in sync by the <see cref="PieceTable"/>.
    /// </summary>
    public int LineFeeds { get; }

    public Piece(BufferType bufferType, long start, long length, int lineFeeds)
    {
        BufferType = bufferType;
        Start = start;
        Length = length;
        LineFeeds = lineFeeds;
    }

    public bool Equals(Piece other) =>
        BufferType == other.BufferType &&
        Start == other.Start &&
        Length == other.Length &&
        LineFeeds == other.LineFeeds;

    public override bool Equals(object? obj) => obj is Piece other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(BufferType, Start, Length, LineFeeds);

    public static bool operator ==(Piece left, Piece right) => left.Equals(right);
    public static bool operator !=(Piece left, Piece right) => !left.Equals(right);

    public override string ToString() =>
        $"Piece({BufferType}, Start={Start}, Len={Length}, LF={LineFeeds})";
}
