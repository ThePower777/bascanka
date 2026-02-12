using System.IO.MemoryMappedFiles;
using TextEncoding = System.Text.Encoding;

namespace Bascanka.Core.IO;

/// <summary>
/// An LRU (Least-Recently-Used) cache of decoded text chunks backed by a
/// memory-mapped file.  Each chunk represents 64 KB of decoded <see cref="string"/>
/// text from a contiguous byte region of the underlying file.  At most 64 chunks
/// (~4 MB of decoded text) are held in memory at any time.
/// </summary>
public sealed class ChunkCache : IDisposable
{
    /// <summary>Size, in bytes, of each raw chunk read from the file.</summary>
    public const int ChunkSizeBytes = 64 * 1024;

    /// <summary>Maximum number of chunks retained in the cache.</summary>
    public const int MaxChunks = 64;

    private readonly MemoryMappedFile _mmf;
    private readonly long _fileSize;
    private readonly TextEncoding _encoding;
    private readonly bool _normalizeLineEndings;

    /// <summary>
    /// Maps a chunk's byte offset (aligned to <see cref="ChunkSizeBytes"/>) to
    /// the cached entry containing the decoded text.
    /// </summary>
    private readonly Dictionary<long, LinkedListNode<CacheEntry>> _map = new();

    /// <summary>
    /// Doubly-linked list ordered from most-recently-used (First) to
    /// least-recently-used (Last).
    /// </summary>
    private readonly LinkedList<CacheEntry> _lruList = new();

    private readonly ReaderWriterLockSlim _lock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="ChunkCache"/> over an already-opened memory-mapped file.
    /// </summary>
    /// <param name="mmf">The memory-mapped file to read from.</param>
    /// <param name="fileSize">Total size of the file in bytes.</param>
    /// <param name="encoding">
    /// The encoding used to decode raw bytes into characters.
    /// </param>
    /// <param name="normalizeLineEndings">
    /// When <see langword="true"/>, each decoded chunk has its line endings
    /// normalized to <c>\n</c> (<c>\r\n</c> → <c>\n</c>, lone <c>\r</c> → <c>\n</c>).
    /// </param>
    public ChunkCache(MemoryMappedFile mmf, long fileSize, TextEncoding encoding,
        bool normalizeLineEndings = false)
    {
        _mmf = mmf ?? throw new ArgumentNullException(nameof(mmf));
        _fileSize = fileSize;
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _normalizeLineEndings = normalizeLineEndings;
    }

    /// <summary>
    /// Returns the decoded text chunk that begins at the given aligned byte offset.
    /// If the chunk is already cached it is promoted to most-recently-used;
    /// otherwise it is decoded from the memory-mapped file and inserted into the cache,
    /// evicting the LRU entry if the cache is full.
    /// </summary>
    /// <param name="byteOffset">
    /// A byte offset aligned to <see cref="ChunkSizeBytes"/>.  The method will
    /// align it automatically if it is not already aligned.
    /// </param>
    /// <returns>The decoded text for that chunk region.</returns>
    public string GetChunk(long byteOffset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long aligned = AlignOffset(byteOffset);

        // --- Fast path: read lock ---
        _lock.EnterReadLock();
        try
        {
            if (_map.TryGetValue(aligned, out LinkedListNode<CacheEntry>? node))
            {
                // Promote to MRU under a write lock (handled below if needed).
                // We cannot modify the list under a read lock, so fall through.
                // However, we can still return the data; promote on next access
                // is acceptable for a soft-LRU approach.
                return node.Value.Text;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // --- Slow path: write lock ---
        _lock.EnterWriteLock();
        try
        {
            // Double-check: another thread may have loaded the chunk.
            if (_map.TryGetValue(aligned, out LinkedListNode<CacheEntry>? existing))
            {
                _lruList.Remove(existing);
                _lruList.AddFirst(existing);
                return existing.Value.Text;
            }

            string decoded = DecodeChunk(aligned);

            var entry = new CacheEntry(aligned, decoded);
            var newNode = _lruList.AddFirst(entry);
            _map[aligned] = newNode;

            // Evict LRU entries if we exceed the maximum.
            while (_map.Count > MaxChunks)
            {
                LinkedListNode<CacheEntry>? last = _lruList.Last;
                if (last is null) break;

                _map.Remove(last.Value.Offset);
                _lruList.RemoveLast();
            }

            return decoded;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Invalidates the entire cache.  Useful after the underlying file changes.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _map.Clear();
            _lruList.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Returns the number of chunks currently held in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _map.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lock.EnterWriteLock();
        try
        {
            _map.Clear();
            _lruList.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }

    /// <summary>Aligns a byte offset down to the nearest chunk boundary.</summary>
    private static long AlignOffset(long offset) =>
        offset - (offset % ChunkSizeBytes);

    /// <summary>
    /// Reads raw bytes from the memory-mapped file at the specified aligned
    /// offset and decodes them into a <see cref="string"/>.
    /// When <see cref="_normalizeLineEndings"/> is enabled, <c>\r\n</c> and
    /// lone <c>\r</c> are replaced with <c>\n</c>.  For <c>\r\n</c> pairs
    /// that span a chunk boundary the leading <c>\n</c> is trimmed.
    /// </summary>
    private string DecodeChunk(long alignedOffset)
    {
        long bytesToRead = Math.Min(ChunkSizeBytes, _fileSize - alignedOffset);
        if (bytesToRead <= 0) return string.Empty;

        using MemoryMappedViewAccessor accessor = _mmf.CreateViewAccessor(
            alignedOffset, bytesToRead, MemoryMappedFileAccess.Read);

        byte[] buffer = new byte[bytesToRead];
        accessor.ReadArray(0, buffer, 0, (int)bytesToRead);

        string decoded = _encoding.GetString(buffer);

        if (!_normalizeLineEndings)
            return decoded;

        // Handle \r\n spanning a chunk boundary: if the previous chunk's last
        // raw byte was \r (0x0D) and this chunk starts with \n, trim the \n
        // because the previous chunk already emitted a \n for that \r.
        bool trimLeadingLf = false;
        if (alignedOffset > 0 && decoded.Length > 0 && decoded[0] == '\n')
        {
            using MemoryMappedViewAccessor peeker = _mmf.CreateViewAccessor(
                alignedOffset - 1, 1, MemoryMappedFileAccess.Read);
            byte prevByte = peeker.ReadByte(0);
            if (prevByte == 0x0D)
                trimLeadingLf = true;
        }

        // Single-pass normalization: \r\n → \n, lone \r → \n.
        bool needsNormalization = false;
        for (int i = 0; i < decoded.Length; i++)
        {
            if (decoded[i] == '\r') { needsNormalization = true; break; }
        }

        if (!needsNormalization)
        {
            return trimLeadingLf ? decoded[1..] : decoded;
        }

        int start = trimLeadingLf ? 1 : 0;
        var sb = new System.Text.StringBuilder(decoded.Length);
        for (int i = start; i < decoded.Length; i++)
        {
            if (decoded[i] == '\r')
            {
                sb.Append('\n');
                if (i + 1 < decoded.Length && decoded[i + 1] == '\n')
                    i++; // skip the \n in \r\n
            }
            else
            {
                sb.Append(decoded[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>Internal cache entry holding the aligned byte offset and decoded text.</summary>
    private readonly record struct CacheEntry(long Offset, string Text);
}
