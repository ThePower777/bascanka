namespace Bascanka.Core.Syntax;

/// <summary>
/// Tokenizes a single line of source text, producing a list of <see cref="Token"/>
/// values that the rendering layer maps to colours.
///
/// Implementations must be stateless between calls: all inter-line state is
/// carried explicitly through <see cref="LexerState"/>.
/// </summary>
public interface ILexer
{
    /// <summary>
    /// A unique identifier for the language this lexer handles (e.g. <c>"csharp"</c>,
    /// <c>"javascript"</c>).  Used as a key in <see cref="LexerRegistry"/>.
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// File extensions associated with this language, including the leading dot
    /// (e.g. <c>".cs"</c>, <c>".js"</c>).
    /// </summary>
    string[] FileExtensions { get; }

    /// <summary>
    /// Tokenizes a single line of text.
    /// </summary>
    /// <param name="line">
    /// The full text of the line, excluding the line terminator.
    /// </param>
    /// <param name="startState">
    /// The lexer state inherited from the end of the previous line.
    /// For the first line of a file, pass <see cref="LexerState.Normal"/>.
    /// </param>
    /// <returns>
    /// A tuple containing the list of tokens found on this line and the
    /// lexer state at the end of the line (to be passed as <paramref name="startState"/>
    /// for the next line).
    /// </returns>
    (List<Token> tokens, LexerState endState) Tokenize(string line, LexerState startState);
}
