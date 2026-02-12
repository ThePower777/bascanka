using Bascanka.Core.Buffer;

namespace Bascanka.Core.Syntax;

/// <summary>
/// Caches per-line tokenization results so that only lines affected by an
/// edit need to be re-lexed.  The cache stores the token list and the
/// end-of-line <see cref="LexerState"/> for every line that has been tokenized
/// at least once.
/// </summary>
public sealed class TokenCache
{
    private readonly struct LineCacheEntry
    {
        public List<Token>? Tokens { get; init; }
        public LexerState EndState { get; init; }
        public bool IsValid { get; init; }
    }

    private readonly List<LineCacheEntry> _entries = new();
    private readonly object _lock = new();

    /// <summary>
    /// Invalidates cached data for <paramref name="count"/> lines starting at
    /// <paramref name="startLine"/>.  The entries remain in the list but are
    /// marked invalid so they will be re-tokenized on next access.
    /// </summary>
    public void Invalidate(long startLine, long count)
    {
        lock (_lock)
        {
            long end = Math.Min(startLine + count, _entries.Count);
            for (long i = startLine; i < end; i++)
            {
                _entries[(int)i] = default; // IsValid == false
            }
        }
    }

    /// <summary>
    /// Inserts <paramref name="count"/> empty (invalid) entries at <paramref name="line"/>
    /// to keep the cache aligned with the document after lines are inserted.
    /// </summary>
    public void InsertLines(long line, long count)
    {
        lock (_lock)
        {
            int idx = (int)Math.Min(line, _entries.Count);
            for (long i = 0; i < count; i++)
            {
                _entries.Insert(idx, default);
            }
        }
    }

    /// <summary>
    /// Removes <paramref name="count"/> entries at <paramref name="line"/>
    /// to keep the cache aligned with the document after lines are deleted.
    /// </summary>
    public void DeleteLines(long line, long count)
    {
        lock (_lock)
        {
            int idx = (int)line;
            int toRemove = (int)Math.Min(count, _entries.Count - idx);
            if (toRemove > 0)
            {
                _entries.RemoveRange(idx, toRemove);
            }
        }
    }

    /// <summary>
    /// Returns the cached end-state for <paramref name="lineIndex"/>, or
    /// <see langword="null"/> if the line has not been cached or was invalidated.
    /// </summary>
    public LexerState? GetCachedState(long lineIndex)
    {
        lock (_lock)
        {
            if (lineIndex < 0 || lineIndex >= _entries.Count)
                return null;

            var entry = _entries[(int)lineIndex];
            return entry.IsValid ? entry.EndState : null;
        }
    }

    /// <summary>
    /// Returns the cached tokens for <paramref name="lineIndex"/>, or
    /// <see langword="null"/> if the line has not been cached or was invalidated.
    /// </summary>
    public List<Token>? GetCachedTokens(long lineIndex)
    {
        lock (_lock)
        {
            if (lineIndex < 0 || lineIndex >= _entries.Count)
                return null;

            var entry = _entries[(int)lineIndex];
            return entry.IsValid ? entry.Tokens : null;
        }
    }

    /// <summary>
    /// Stores the tokenization result for <paramref name="lineIndex"/>.
    /// </summary>
    public void SetCache(long lineIndex, List<Token> tokens, LexerState endState)
    {
        lock (_lock)
        {
            // Grow the list if necessary.
            while (_entries.Count <= lineIndex)
            {
                _entries.Add(default);
            }

            _entries[(int)lineIndex] = new LineCacheEntry
            {
                Tokens = tokens,
                EndState = endState,
                IsValid = true,
            };
        }
    }

    /// <summary>
    /// Performs incremental re-lexing starting at <paramref name="editLine"/>.
    /// Walks forward through the document, re-tokenizing each line until the
    /// computed end-state matches the previously cached end-state (meaning all
    /// subsequent lines are still valid) or the end of the document is reached.
    /// Uses batch line fetching for efficiency.
    /// </summary>
    /// <param name="editLine">The zero-based line index where the edit occurred.</param>
    /// <param name="buffer">The document buffer, used to retrieve line text.</param>
    /// <param name="lexer">The lexer to use for tokenization.</param>
    public void IncrementalRelex(long editLine, PieceTable buffer, ILexer lexer)
    {
        long lineCount = buffer.LineCount;
        if (editLine >= lineCount)
            return;

        // Determine the start state: use the end-state of the preceding line
        // if available, otherwise start from Normal.
        LexerState state;
        if (editLine > 0)
        {
            LexerState? prev = GetCachedState(editLine - 1);
            state = prev ?? LexerState.Normal;
        }
        else
        {
            state = LexerState.Normal;
        }

        // Fetch lines in batches to avoid per-line tree lookups.
        const int BatchSize = 64;
        long pos = editLine;

        while (pos < lineCount)
        {
            int count = (int)Math.Min(BatchSize, lineCount - pos);
            var lines = buffer.GetLineRange(pos, count);

            bool done = false;
            for (int i = 0; i < lines.Length; i++)
            {
                long lineIdx = pos + i;
                var (tokens, endState) = lexer.Tokenize(lines[i].Text, state);

                // Check whether the computed end-state matches what was cached.
                // If it does, all subsequent lines are unaffected and we can stop.
                LexerState? cached = GetCachedState(lineIdx);
                SetCache(lineIdx, tokens, endState);

                if (cached.HasValue && cached.Value == endState && lineIdx > editLine)
                {
                    done = true;
                    break;
                }

                state = endState;
            }

            if (done) break;
            pos += lines.Length;
        }
    }

    /// <summary>
    /// Clears the entire cache.  Typically called when the language mode changes
    /// or the document is replaced.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
