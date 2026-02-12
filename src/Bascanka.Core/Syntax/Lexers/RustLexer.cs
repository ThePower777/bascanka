namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for Rust.  Handles keywords, lifetime annotations (<c>'a</c>),
/// raw strings (<c>r#"..."#</c>), attributes (<c>#[...]</c>), macros
/// (<c>name!</c>), comments (<c>//</c> and nestable <c>/* */</c>), and
/// numeric literals with type suffixes.
/// </summary>
public sealed class RustLexer : BaseLexer
{
    private const int StateInRawString = 10;

    public override string LanguageId => "rust";
    public override string[] FileExtensions => [".rs"];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "as", "async", "await", "break", "const", "continue", "crate",
        "dyn", "else", "enum", "extern", "false", "fn", "for", "if",
        "impl", "in", "let", "loop", "match", "mod", "move", "mut",
        "pub", "ref", "return", "self", "Self", "static", "struct",
        "super", "trait", "true", "type", "union", "unsafe", "use",
        "where", "while", "yield", "abstract", "become", "box", "do",
        "final", "macro", "override", "priv", "try", "typeof", "unsized",
        "virtual",
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal)
    {
        "bool", "char", "f32", "f64", "i8", "i16", "i32", "i64", "i128",
        "isize", "str", "u8", "u16", "u32", "u64", "u128", "usize",
        "String", "Vec", "Box", "Rc", "Arc", "Cell", "RefCell", "Mutex",
        "RwLock", "HashMap", "HashSet", "BTreeMap", "BTreeSet", "Option",
        "Result", "Ok", "Err", "Some", "None", "Pin", "Cow", "Fn",
        "FnMut", "FnOnce", "Send", "Sync", "Sized", "Copy", "Clone",
        "Debug", "Display", "Default", "Iterator", "IntoIterator",
        "From", "Into", "TryFrom", "TryInto", "AsRef", "AsMut",
        "Drop", "Deref", "DerefMut", "Index", "IndexMut",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Single-line comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '/')
        {
            ReadLineComment(line, ref pos, tokens);
            return state;
        }

        // Nestable block comment /* ... */ .
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state, "/*", "*/", trackNesting: true);
        }

        // Attribute: #[...] or #![...]
        if (c == '#' && pos + 1 < line.Length && (line[pos + 1] == '[' || (line[pos + 1] == '!' && pos + 2 < line.Length && line[pos + 2] == '[')))
        {
            int start = pos;
            pos++; // skip #
            if (pos < line.Length && line[pos] == '!')
                pos++; // skip !
            if (pos < line.Length && line[pos] == '[')
            {
                pos++; // skip [
                int depth = 1;
                while (pos < line.Length && depth > 0)
                {
                    if (line[pos] == '[') depth++;
                    else if (line[pos] == ']') depth--;
                    if (depth > 0) pos++;
                }
                if (pos < line.Length) pos++; // skip closing ]
            }
            tokens.Add(new Token(start, pos - start, TokenType.Attribute));
            return state;
        }

        // Raw string literal: r"..." or r#"..."# etc.
        if (c == 'r' && pos + 1 < line.Length && (line[pos + 1] == '"' || line[pos + 1] == '#'))
        {
            return ReadRawString(line, ref pos, tokens);
        }

        // Byte string: b"..." or b'...'
        if (c == 'b' && pos + 1 < line.Length && (line[pos + 1] == '"' || line[pos + 1] == '\''))
        {
            int start = pos;
            pos++; // skip b
            if (line[pos] == '"')
                ReadString(line, ref pos, tokens, '"');
            else
                ReadCharLiteral(line, ref pos, tokens);
            // Adjust start of the last token to include the b prefix.
            var last = tokens[^1];
            tokens[^1] = new Token(start, pos - start, last.Type);
            return state;
        }

        // String.
        if (c == '"')
        {
            ReadString(line, ref pos, tokens, '"');
            return state;
        }

        // Lifetime or char literal.
        if (c == '\'')
        {
            // Lifetime: 'a, 'static, 'self, etc.
            if (pos + 1 < line.Length && IsIdentStart(line[pos + 1]))
            {
                int start = pos;
                pos++; // skip '
                while (pos < line.Length && IsIdentPart(line[pos]))
                    pos++;

                // If followed by another ', it is a char literal like 'a'.
                if (pos < line.Length && line[pos] == '\'')
                {
                    pos++;
                    tokens.Add(new Token(start, pos - start, TokenType.Character));
                }
                else
                {
                    // It is a lifetime annotation.
                    tokens.Add(new Token(start, pos - start, TokenType.Attribute));
                }
                return state;
            }

            // Regular char literal.
            ReadCharLiteral(line, ref pos, tokens);
            return state;
        }

        // Numbers.
        if (char.IsDigit(c))
        {
            ReadNumber(line, ref pos, tokens);
            return state;
        }

        // Identifiers, keywords, and macros.
        if (IsIdentStart(c))
        {
            int start = pos;
            int len = ReadIdentifierLength(line, pos);
            string word = line.Substring(pos, len);
            pos += len;

            // Check for macro invocation: word!
            if (pos < line.Length && line[pos] == '!')
            {
                pos++; // include !
                tokens.Add(new Token(start, pos - start, TokenType.Keyword));
                return state;
            }

            TokenType type;
            if (Keywords.Contains(word))
                type = TokenType.Keyword;
            else if (TypeNames.Contains(word))
                type = TokenType.TypeName;
            else if (char.IsUpper(word[0]))
                type = TokenType.TypeName;
            else
                type = TokenType.Identifier;

            tokens.Add(new Token(start, pos - start, type));
            return state;
        }

        // Operators.
        if (IsOperatorChar(c) || c == '.' || c == '@')
        {
            int len = 1;
            if (pos + 2 < line.Length)
            {
                string three = line.Substring(pos, 3);
                if (three is "<<=" or ">>=" or "..=" or "...")
                    len = 3;
            }
            if (len == 1 && pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "+=" or "-=" or "*=" or "/=" or "%=" or "&=" or "|=" or
                    "^=" or "<<" or ">>" or ".." or "=>" or "->" or "::")
                    len = 2;
            }
            EmitOperator(line, ref pos, tokens, len);
            return state;
        }

        // Punctuation.
        if (IsPunctuation(c) || c == ']')
        {
            EmitPunctuation(line, ref pos, tokens);
            return state;
        }

        tokens.Add(new Token(pos, 1, TokenType.Plain));
        pos++;
        return state;
    }

    protected override LexerState ContinueMultiLineState(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        return state.StateId switch
        {
            LexerState.StateInMultiLineComment => ReadBlockComment(line, ref pos, tokens, state, "/*", "*/", trackNesting: true),
            StateInRawString => ContinueRawString(line, ref pos, tokens, state.NestingDepth),
            _ => state,
        };
    }

    // ── Raw string literal ──────────────────────────────────────────────

    private static LexerState ReadRawString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip r

        // Count the number of # before the opening ".
        int hashCount = 0;
        while (pos < line.Length && line[pos] == '#')
        {
            hashCount++;
            pos++;
        }

        if (pos >= line.Length || line[pos] != '"')
        {
            // Not actually a raw string -- treat as identifier.
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return LexerState.Normal;
        }

        pos++; // skip "

        // Build closing delimiter: "###
        string closing = "\"" + new string('#', hashCount);

        while (pos < line.Length)
        {
            if (line[pos] == '"' && StartsWith(line, pos, closing))
            {
                pos += closing.Length;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            pos++;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        return new LexerState(StateInRawString, hashCount);
    }

    private static LexerState ContinueRawString(string line, ref int pos, List<Token> tokens, int hashCount)
    {
        int start = pos;
        string closing = "\"" + new string('#', hashCount);

        while (pos < line.Length)
        {
            if (line[pos] == '"' && StartsWith(line, pos, closing))
            {
                pos += closing.Length;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            pos++;
        }

        tokens.Add(new Token(start, line.Length - start, TokenType.String));
        return new LexerState(StateInRawString, hashCount);
    }

    // ── Character literal ───────────────────────────────────────────────

    private static void ReadCharLiteral(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip '

        if (pos < line.Length && line[pos] == '\\')
        {
            pos++; // skip backslash
            if (pos < line.Length)
            {
                char esc = line[pos];
                pos++;
                if (esc == 'x')
                {
                    for (int i = 0; i < 2 && pos < line.Length && IsHexDigit(line[pos]); i++) pos++;
                }
                else if (esc == 'u')
                {
                    if (pos < line.Length && line[pos] == '{')
                    {
                        pos++;
                        while (pos < line.Length && line[pos] != '}' && IsHexDigit(line[pos])) pos++;
                        if (pos < line.Length && line[pos] == '}') pos++;
                    }
                }
            }
        }
        else if (pos < line.Length)
        {
            pos++;
        }

        if (pos < line.Length && line[pos] == '\'')
            pos++;

        tokens.Add(new Token(start, pos - start, TokenType.Character));
    }
}
