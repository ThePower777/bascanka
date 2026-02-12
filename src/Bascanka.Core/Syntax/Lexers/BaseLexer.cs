namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Abstract base class for lexers.  Provides reusable helper methods for common
/// lexing patterns such as reading identifiers, numbers, strings, and comments.
/// Subclasses override <see cref="TokenizeNormal"/> to handle language-specific
/// constructs while relying on the base helpers for the boilerplate.
/// </summary>
public abstract class BaseLexer : ILexer
{
    /// <inheritdoc />
    public abstract string LanguageId { get; }

    /// <inheritdoc />
    public abstract string[] FileExtensions { get; }

    /// <inheritdoc />
    public virtual (List<Token> tokens, LexerState endState) Tokenize(string line, LexerState startState)
    {
        var tokens = new List<Token>();
        int pos = 0;

        LexerState state = startState;

        // If we are continuing a multi-line construct from the previous line,
        // let the subclass handle it first.
        if (state.StateId != LexerState.StateNormal)
        {
            state = ContinueMultiLineState(line, ref pos, tokens, state);
        }

        while (pos < line.Length)
        {
            int before = pos;
            if (state.StateId != LexerState.StateNormal)
                state = ContinueMultiLineState(line, ref pos, tokens, state);
            else
                state = TokenizeNormal(line, ref pos, tokens, state);
            // Safety: if no progress was made, emit the character as plain and advance.
            if (pos == before)
            {
                tokens.Add(new Token(pos, 1, TokenType.Plain));
                pos++;
            }
        }

        return (tokens, state);
    }

    /// <summary>
    /// Called when the lexer is in normal mode (not inside a multi-line construct).
    /// The subclass should consume one token from <paramref name="line"/> starting
    /// at <paramref name="pos"/>, add it to <paramref name="tokens"/>, advance
    /// <paramref name="pos"/>, and return the (possibly changed) state.
    /// </summary>
    protected abstract LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state);

    /// <summary>
    /// Called at the start of a line when the inherited state is non-normal.
    /// The subclass should continue consuming the multi-line construct.
    /// The default implementation does nothing (returns the state unchanged).
    /// </summary>
    protected virtual LexerState ContinueMultiLineState(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        return state;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: keyword matching
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starting at <paramref name="pos"/>, reads an identifier-like word and
    /// checks it against <paramref name="keywords"/>.  Returns <see langword="true"/>
    /// if the word is a keyword, and outputs the word length.
    /// </summary>
    protected static bool MatchKeyword(
        string line, int pos, HashSet<string> keywords, out int length)
    {
        length = ReadIdentifierLength(line, pos);
        if (length == 0) return false;

        ReadOnlySpan<char> word = line.AsSpan(pos, length);
        return keywords.Contains(word.ToString());
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: skip whitespace
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Skips whitespace starting at <paramref name="pos"/>, emitting a single
    /// <see cref="TokenType.Plain"/> token for the entire run.
    /// Returns <see langword="true"/> if any whitespace was skipped.
    /// </summary>
    protected static bool SkipWhitespace(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        while (pos < line.Length && char.IsWhiteSpace(line[pos]))
            pos++;

        if (pos > start)
        {
            tokens.Add(new Token(start, pos - start, TokenType.Plain));
            return true;
        }
        return false;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: read while predicate
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances <paramref name="pos"/> while <paramref name="predicate"/> is
    /// true and returns the number of characters consumed.
    /// </summary>
    protected static int ReadWhile(string line, ref int pos, Func<char, bool> predicate)
    {
        int start = pos;
        while (pos < line.Length && predicate(line[pos]))
            pos++;
        return pos - start;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: identifiers
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads an identifier (letter/underscore followed by letters, digits, or
    /// underscores) and returns its length without advancing <paramref name="pos"/>.
    /// </summary>
    protected static int ReadIdentifierLength(string line, int pos)
    {
        if (pos >= line.Length) return 0;
        if (!IsIdentStart(line[pos])) return 0;

        int i = pos + 1;
        while (i < line.Length && IsIdentPart(line[i]))
            i++;
        return i - pos;
    }

    /// <summary>
    /// Reads an identifier starting at <paramref name="pos"/>, adds it as
    /// <paramref name="type"/>, and advances <paramref name="pos"/>.
    /// Returns <see langword="true"/> if an identifier was consumed.
    /// </summary>
    protected static bool ReadIdentifier(
        string line, ref int pos, List<Token> tokens, TokenType type = TokenType.Identifier)
    {
        int len = ReadIdentifierLength(line, pos);
        if (len == 0) return false;

        tokens.Add(new Token(pos, len, type));
        pos += len;
        return true;
    }

    /// <summary>
    /// Reads an identifier, checks it against two keyword sets (primary keywords
    /// and type-name keywords), and emits the correct token type.
    /// </summary>
    protected static void ReadIdentifierOrKeyword(
        string line,
        ref int pos,
        List<Token> tokens,
        HashSet<string> keywords,
        HashSet<string>? typeNames = null)
    {
        int len = ReadIdentifierLength(line, pos);
        if (len == 0) return;

        string word = line.Substring(pos, len);
        TokenType type;

        if (keywords.Contains(word))
            type = TokenType.Keyword;
        else if (typeNames != null && typeNames.Contains(word))
            type = TokenType.TypeName;
        else if (len > 0 && char.IsUpper(word[0]))
            type = TokenType.TypeName;
        else
            type = TokenType.Identifier;

        tokens.Add(new Token(pos, len, type));
        pos += len;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: strings
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a single-line string delimited by <paramref name="quote"/>,
    /// handling <c>\</c> escape sequences.  If the closing quote is not found
    /// the token extends to end-of-line and the caller should update state.
    /// Returns <see langword="true"/> if the string was fully closed on this line.
    /// </summary>
    protected static bool ReadString(
        string line, ref int pos, List<Token> tokens, char quote, bool emitEscapes = false)
    {
        int start = pos;
        pos++; // skip opening quote

        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length)
            {
                if (emitEscapes)
                {
                    // Emit string portion before escape.
                    if (pos > start)
                        tokens.Add(new Token(start, pos - start, TokenType.String));

                    int escLen = 2;
                    tokens.Add(new Token(pos, escLen, TokenType.Escape));
                    pos += escLen;
                    start = pos;
                }
                else
                {
                    pos += 2; // skip escape sequence
                }
            }
            else if (line[pos] == quote)
            {
                pos++; // skip closing quote
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return true;
            }
            else
            {
                pos++;
            }
        }

        // Unterminated string.
        tokens.Add(new Token(start, pos - start, TokenType.String));
        return false;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: comments
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a single-line comment starting at <paramref name="pos"/> (which
    /// should point to the first character of the comment leader, e.g. <c>//</c>
    /// or <c>#</c>).  Consumes to end of line.
    /// </summary>
    protected static void ReadLineComment(
        string line, ref int pos, List<Token> tokens, int leaderLength = 2)
    {
        int start = pos;
        pos = line.Length;
        tokens.Add(new Token(start, pos - start, TokenType.Comment));
    }

    /// <summary>
    /// Reads a block comment (<c>/* ... */</c> style).  If the closing
    /// <c>*/</c> is found on this line, returns <see cref="LexerState.Normal"/>.
    /// Otherwise returns a state indicating the comment continues.
    /// </summary>
    protected static LexerState ReadBlockComment(
        string line, ref int pos, List<Token> tokens, LexerState state,
        string open = "/*", string close = "*/", bool trackNesting = false)
    {
        int start = pos;
        int depth = state.StateId == LexerState.StateInMultiLineComment
            ? Math.Max(state.NestingDepth, 1)
            : 1;

        // If we are starting fresh (not continuing), skip past the opening delimiter.
        if (state.StateId != LexerState.StateInMultiLineComment)
        {
            pos += open.Length;
        }

        while (pos < line.Length)
        {
            if (trackNesting && pos + open.Length <= line.Length &&
                line.AsSpan(pos, open.Length).SequenceEqual(open))
            {
                depth++;
                pos += open.Length;
            }
            else if (pos + close.Length <= line.Length &&
                     line.AsSpan(pos, close.Length).SequenceEqual(close))
            {
                depth--;
                pos += close.Length;
                if (depth <= 0)
                {
                    tokens.Add(new Token(start, pos - start, TokenType.MultiLineComment));
                    return LexerState.Normal;
                }
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.MultiLineComment));
        return new LexerState(LexerState.StateInMultiLineComment, depth);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: numbers
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a numeric literal: decimal, hex (<c>0x</c>), binary (<c>0b</c>),
    /// octal (<c>0o</c>), and floating-point with optional exponent and suffix.
    /// </summary>
    protected static void ReadNumber(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;

        if (pos + 1 < line.Length && line[pos] == '0')
        {
            char next = char.ToLower(line[pos + 1]);
            if (next == 'x')
            {
                pos += 2;
                while (pos < line.Length && IsHexDigit(line[pos])) pos++;
                ReadNumericSuffix(line, ref pos);
                tokens.Add(new Token(start, pos - start, TokenType.Number));
                return;
            }
            if (next == 'b')
            {
                pos += 2;
                while (pos < line.Length && (line[pos] == '0' || line[pos] == '1' || line[pos] == '_')) pos++;
                ReadNumericSuffix(line, ref pos);
                tokens.Add(new Token(start, pos - start, TokenType.Number));
                return;
            }
            if (next == 'o')
            {
                pos += 2;
                while (pos < line.Length && line[pos] >= '0' && line[pos] <= '7') pos++;
                ReadNumericSuffix(line, ref pos);
                tokens.Add(new Token(start, pos - start, TokenType.Number));
                return;
            }
        }

        // Decimal / float.
        while (pos < line.Length && (char.IsDigit(line[pos]) || line[pos] == '_'))
            pos++;

        if (pos < line.Length && line[pos] == '.')
        {
            // Check that the next char is a digit (avoid matching member access like 42.ToString).
            if (pos + 1 < line.Length && char.IsDigit(line[pos + 1]))
            {
                pos++; // skip dot
                while (pos < line.Length && (char.IsDigit(line[pos]) || line[pos] == '_'))
                    pos++;
            }
        }

        // Exponent.
        if (pos < line.Length && (line[pos] == 'e' || line[pos] == 'E'))
        {
            pos++;
            if (pos < line.Length && (line[pos] == '+' || line[pos] == '-'))
                pos++;
            while (pos < line.Length && char.IsDigit(line[pos]))
                pos++;
        }

        ReadNumericSuffix(line, ref pos);
        tokens.Add(new Token(start, pos - start, TokenType.Number));
    }

    private static void ReadNumericSuffix(string line, ref int pos)
    {
        // Common suffixes: u, l, ul, lu, f, d, m (C#), ll, ull (C/C++).
        while (pos < line.Length && (char.IsLetter(line[pos]) || line[pos] == '_'))
        {
            char c = char.ToLower(line[pos]);
            if (c == 'u' || c == 'l' || c == 'f' || c == 'd' || c == 'm' ||
                c == 'i' || c == 'z' || c == 'n')
                pos++;
            else
                break;
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helper: operators and punctuation
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emits a single character as an operator token.
    /// </summary>
    protected static void EmitOperator(string line, ref int pos, List<Token> tokens, int length = 1)
    {
        tokens.Add(new Token(pos, length, TokenType.Operator));
        pos += length;
    }

    /// <summary>
    /// Emits a single character as a punctuation token.
    /// </summary>
    protected static void EmitPunctuation(string line, ref int pos, List<Token> tokens, int length = 1)
    {
        tokens.Add(new Token(pos, length, TokenType.Punctuation));
        pos += length;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Character classification
    // ────────────────────────────────────────────────────────────────────

    protected static bool IsIdentStart(char c) =>
        char.IsLetter(c) || c == '_';

    protected static bool IsIdentPart(char c) =>
        char.IsLetterOrDigit(c) || c == '_';

    protected static bool IsHexDigit(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || c == '_';

    protected static bool IsOperatorChar(char c) =>
        "+-*/%=<>!&|^~?:".Contains(c);

    protected static bool IsPunctuation(char c) =>
        "(){}[];,.".Contains(c);

    /// <summary>
    /// Checks whether <paramref name="line"/> at <paramref name="pos"/> starts
    /// with <paramref name="prefix"/>.
    /// </summary>
    protected static bool StartsWith(string line, int pos, string prefix)
    {
        if (pos + prefix.Length > line.Length) return false;
        return line.AsSpan(pos, prefix.Length).SequenceEqual(prefix);
    }
}
