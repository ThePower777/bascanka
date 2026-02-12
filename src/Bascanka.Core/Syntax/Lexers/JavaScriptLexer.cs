namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for JavaScript.  Handles keywords, template literals with
/// interpolation (<c>`...${expr}...`</c>), regular expression literals,
/// single/double-quoted strings, and <c>//</c> / <c>/* */</c> comments.
/// </summary>
public class JavaScriptLexer : BaseLexer
{
    public override string LanguageId => "javascript";
    public override string[] FileExtensions => [".js", ".mjs", ".cjs", ".jsx"];

    private static readonly HashSet<string> JsKeywords = new(StringComparer.Ordinal)
    {
        "async", "await", "break", "case", "catch", "class", "const",
        "continue", "debugger", "default", "delete", "do", "else", "export",
        "extends", "false", "finally", "for", "from", "function", "if",
        "import", "in", "instanceof", "let", "new", "null", "of", "return",
        "static", "super", "switch", "this", "throw", "true", "try",
        "typeof", "undefined", "var", "void", "while", "with", "yield",
    };

    protected HashSet<string> Keywords => JsKeywords;

    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal)
    {
        "Array", "Boolean", "Date", "Error", "Function", "Map", "Number",
        "Object", "Promise", "RegExp", "Set", "String", "Symbol", "WeakMap",
        "WeakSet", "BigInt", "Proxy", "Reflect", "JSON", "Math", "console",
        "globalThis", "Intl", "ArrayBuffer", "DataView", "Float32Array",
        "Float64Array", "Int8Array", "Int16Array", "Int32Array", "Uint8Array",
    };

    /// <summary>
    /// Additional keywords that TypeScript adds.  The JS lexer ignores these
    /// but the TS lexer populates them.
    /// </summary>
    protected virtual HashSet<string>? ExtraKeywords => null;

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

        // Multi-line comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }

        // Regex literal.
        if (c == '/' && CanStartRegex(tokens))
        {
            return ReadRegexLiteral(line, ref pos, tokens, state);
        }

        // Template literal.
        if (c == '`')
        {
            return ReadTemplateLiteral(line, ref pos, tokens, state);
        }

        // Strings.
        if (c == '"' || c == '\'')
        {
            ReadString(line, ref pos, tokens, c);
            return state;
        }

        // Numbers.
        if (char.IsDigit(c) || (c == '.' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
        {
            ReadNumber(line, ref pos, tokens);
            return state;
        }

        // Identifiers and keywords.
        if (IsIdentStart(c) || c == '$')
        {
            int start = pos;
            pos++;
            while (pos < line.Length && (IsIdentPart(line[pos]) || line[pos] == '$'))
                pos++;

            string word = line.Substring(start, pos - start);
            TokenType type;

            if (Keywords.Contains(word))
                type = TokenType.Keyword;
            else if (ExtraKeywords != null && ExtraKeywords.Contains(word))
                type = TokenType.Keyword;
            else if (TypeNames.Contains(word))
                type = TokenType.TypeName;
            else if (word.Length > 0 && char.IsUpper(word[0]))
                type = TokenType.TypeName;
            else
                type = TokenType.Identifier;

            tokens.Add(new Token(start, pos - start, type));
            return state;
        }

        // Arrow function.
        if (c == '=' && pos + 1 < line.Length && line[pos + 1] == '>')
        {
            EmitOperator(line, ref pos, tokens, 2);
            return state;
        }

        // Operators.
        if (IsOperatorChar(c))
        {
            int len = 1;
            if (pos + 2 < line.Length)
            {
                string three = line.Substring(pos, 3);
                if (three is "===" or "!==" or ">>>" or "**=" or "&&=" or "||=" or "??=")
                    len = 3;
            }
            if (len == 1 && pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "++" or "--" or "+=" or "-=" or "*=" or "/=" or "**" or
                    "??" or "?." or "<<" or ">>" or "&=" or "|=" or "^=")
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
            LexerState.StateInMultiLineComment => ReadBlockComment(line, ref pos, tokens, state),
            LexerState.StateInTemplateLiteral => ContinueTemplateLiteral(line, ref pos, tokens, state),
            _ => state,
        };
    }

    // ── Template literals ───────────────────────────────────────────────

    private static LexerState ReadTemplateLiteral(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        int start = pos;
        pos++; // skip opening `

        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (line[pos] == '$' && pos + 1 < line.Length && line[pos + 1] == '{')
            {
                // Emit the template part up to the interpolation.
                tokens.Add(new Token(start, pos - start, TokenType.String));
                EmitPunctuation(line, ref pos, tokens, 2); // ${
                // The rest is normal code until } -- for simplicity we
                // track brace depth in NestingDepth.
                return new LexerState(LexerState.StateInTemplateLiteral, 1);
            }
            else if (line[pos] == '`')
            {
                pos++;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(LexerState.StateInTemplateLiteral, 0);
    }

    private LexerState ContinueTemplateLiteral(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        int depth = state.NestingDepth;

        // If depth > 0, we are inside an interpolation -- lex as normal code
        // until braces balance.
        if (depth > 0)
        {
            while (pos < line.Length)
            {
                char c = line[pos];
                if (c == '{')
                {
                    depth++;
                    EmitPunctuation(line, ref pos, tokens);
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        EmitPunctuation(line, ref pos, tokens);
                        // Resume template literal scanning.
                        return ScanTemplateBody(line, ref pos, tokens);
                    }
                    EmitPunctuation(line, ref pos, tokens);
                }
                else
                {
                    // Lex one normal token.
                    var newState = TokenizeNormal(line, ref pos, tokens, LexerState.Normal);
                    if (newState.StateId == LexerState.StateInMultiLineComment)
                        return newState; // comment spans lines -- unusual but handle it
                }
            }
            return new LexerState(LexerState.StateInTemplateLiteral, depth);
        }

        // depth == 0: we are inside the template string body.
        return ScanTemplateBody(line, ref pos, tokens);
    }

    private static LexerState ScanTemplateBody(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;

        while (pos < line.Length)
        {
            if (line[pos] == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (line[pos] == '$' && pos + 1 < line.Length && line[pos + 1] == '{')
            {
                tokens.Add(new Token(start, pos - start, TokenType.String));
                EmitPunctuation(line, ref pos, tokens, 2);
                return new LexerState(LexerState.StateInTemplateLiteral, 1);
            }
            else if (line[pos] == '`')
            {
                pos++;
                tokens.Add(new Token(start, pos - start, TokenType.String));
                return LexerState.Normal;
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
        return new LexerState(LexerState.StateInTemplateLiteral, 0);
    }

    // ── Regex literals ──────────────────────────────────────────────────

    private static LexerState ReadRegexLiteral(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        int start = pos;
        pos++; // skip opening /

        bool inCharClass = false;
        while (pos < line.Length)
        {
            char c = line[pos];
            if (c == '\\' && pos + 1 < line.Length)
            {
                pos += 2;
            }
            else if (c == '[')
            {
                inCharClass = true;
                pos++;
            }
            else if (c == ']')
            {
                inCharClass = false;
                pos++;
            }
            else if (c == '/' && !inCharClass)
            {
                pos++;
                // Read flags.
                while (pos < line.Length && char.IsLetter(line[pos]))
                    pos++;
                tokens.Add(new Token(start, pos - start, TokenType.Regex));
                return state;
            }
            else
            {
                pos++;
            }
        }

        // Unterminated -- treat as operator.
        tokens.Add(new Token(start, 1, TokenType.Operator));
        pos = start + 1;
        return state;
    }

    /// <summary>
    /// Heuristic: a <c>/</c> can start a regex if the previous meaningful
    /// token is an operator, punctuation, keyword, or there are no previous tokens.
    /// </summary>
    private static bool CanStartRegex(List<Token> tokens)
    {
        if (tokens.Count == 0) return true;
        TokenType last = tokens[^1].Type;
        return last is TokenType.Operator or TokenType.Punctuation or
               TokenType.Keyword or TokenType.Plain;
    }
}
