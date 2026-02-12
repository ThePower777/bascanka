namespace Bascanka.Core.Syntax;

/// <summary>
/// Describes a single highlighted span within a line of text.
/// Kept as a small value type to minimise allocation pressure when
/// the rendering layer walks thousands of tokens per frame.
/// </summary>
/// <param name="Start">Zero-based character offset within the line.</param>
/// <param name="Length">Number of characters this token covers.</param>
/// <param name="Type">The syntactic role of the token.</param>
public readonly record struct Token(int Start, int Length, TokenType Type)
{
    /// <summary>
    /// The exclusive end offset of this token within the line.
    /// </summary>
    public int End => Start + Length;
}
