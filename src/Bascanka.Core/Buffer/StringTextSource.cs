namespace Bascanka.Core.Buffer;

/// <summary>
/// An <see cref="ITextSource"/> implementation backed by an in-memory
/// <see cref="string"/>.  Suitable for small-to-medium files that fit
/// comfortably in managed memory.
/// </summary>
public sealed class StringTextSource : ITextSource
{
    private readonly string _data;

    public StringTextSource(string data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <inheritdoc />
    public char this[long index]
    {
        get
        {
            if (index < 0 || index >= _data.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _data[(int)index];
        }
    }

    /// <inheritdoc />
    public long Length => _data.Length;

    /// <inheritdoc />
    public string GetText(long start, long length)
    {
        ValidateRange(start, length);

        if (length == 0)
            return string.Empty;

        return _data.Substring((int)start, (int)length);
    }

    /// <inheritdoc />
    public int CountLineFeeds(long start, long length)
    {
        ValidateRange(start, length);

        int count = 0;
        ReadOnlySpan<char> span = _data.AsSpan((int)start, (int)length);

        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == '\n')
                count++;
        }

        return count;
    }

    private void ValidateRange(long start, long length)
    {
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start), "Start must be non-negative.");
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        if (start + length > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(length), "Range exceeds source length.");
    }
}
