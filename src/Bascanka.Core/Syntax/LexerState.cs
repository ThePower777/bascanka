namespace Bascanka.Core.Syntax;

/// <summary>
/// Captures the lexer state at the end of a line so that the next line can
/// be tokenized correctly.  For example, if a line ends inside a block
/// comment the <see cref="StateId"/> will be non-zero and the following line
/// will continue in that state.
/// </summary>
public readonly struct LexerState : IEquatable<LexerState>
{
    // Well-known state identifiers shared across lexers.
    // Individual lexers may define additional values.
    public const int StateNormal = 0;
    public const int StateInString = 1;
    public const int StateInMultiLineComment = 2;
    public const int StateInMultiLineString = 3;
    public const int StateInTemplateLiteral = 4;
    public const int StateInHeredoc = 5;
    public const int StateInRawString = 6;
    public const int StateInTag = 7;
    public const int StateInCdata = 8;
    public const int StateInFencedCodeBlock = 9;

    /// <summary>
    /// An identifier that describes which multi-line construct the lexer is
    /// currently inside.  <c>0</c> means "normal / top-level".
    /// </summary>
    public int StateId { get; }

    /// <summary>
    /// Tracks nesting depth for constructs that can nest (e.g. nested block
    /// comments in some languages, or brace depth inside template literals).
    /// </summary>
    public int NestingDepth { get; }

    public LexerState(int stateId, int nestingDepth)
    {
        StateId = stateId;
        NestingDepth = nestingDepth;
    }

    /// <summary>
    /// The default start-of-file state: normal mode, no nesting.
    /// </summary>
    public static LexerState Normal => new(StateNormal, 0);

    public bool Equals(LexerState other) =>
        StateId == other.StateId && NestingDepth == other.NestingDepth;

    public override bool Equals(object? obj) => obj is LexerState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(StateId, NestingDepth);

    public static bool operator ==(LexerState left, LexerState right) => left.Equals(right);
    public static bool operator !=(LexerState left, LexerState right) => !left.Equals(right);

    public override string ToString() => $"LexerState(Id={StateId}, Depth={NestingDepth})";
}
