namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for C.  Handles keywords, strings, character literals, comments
/// (<c>//</c> and <c>/* */</c>), preprocessor directives (<c>#include</c>,
/// <c>#define</c>, etc.), and numbers including hex/octal.
/// </summary>
public class CLexer : BaseLexer
{
    public override string LanguageId => "c";
    public override string[] FileExtensions => [".c", ".h"];

    private static readonly HashSet<string> CKeywords = new(StringComparer.Ordinal)
    {
        "auto", "break", "case", "char", "const", "continue", "default",
        "do", "double", "else", "enum", "extern", "float", "for", "goto",
        "if", "inline", "int", "long", "register", "restrict", "return",
        "short", "signed", "sizeof", "static", "struct", "switch",
        "typedef", "union", "unsigned", "void", "volatile", "while",
        "_Alignas", "_Alignof", "_Atomic", "_Bool", "_Complex", "_Generic",
        "_Imaginary", "_Noreturn", "_Static_assert", "_Thread_local",
        "true", "false", "NULL",
    };

    private static readonly HashSet<string> CTypeNames = new(StringComparer.Ordinal)
    {
        "size_t", "ssize_t", "ptrdiff_t", "intptr_t", "uintptr_t",
        "int8_t", "int16_t", "int32_t", "int64_t",
        "uint8_t", "uint16_t", "uint32_t", "uint64_t",
        "FILE", "DIR", "time_t", "clock_t", "pid_t", "off_t",
        "bool", "wchar_t", "char16_t", "char32_t",
    };

    protected virtual HashSet<string> Keywords => CKeywords;
    protected virtual HashSet<string> TypeNames => CTypeNames;

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Preprocessor directive.
        if (c == '#' && IsLineStart(line, pos))
        {
            int start = pos;
            pos = line.Length;
            tokens.Add(new Token(start, pos - start, TokenType.Preprocessor));
            return state;
        }

        // Single-line comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '/')
        {
            ReadLineComment(line, ref pos, tokens);
            return state;
        }

        // Block comment.
        if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
        {
            return ReadBlockComment(line, ref pos, tokens, state);
        }

        // Strings.
        if (c == '"')
        {
            ReadString(line, ref pos, tokens, '"');
            return state;
        }

        // Character literal.
        if (c == '\'')
        {
            ReadCharLiteral(line, ref pos, tokens);
            return state;
        }

        // Numbers.
        if (char.IsDigit(c) || (c == '.' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
        {
            ReadNumber(line, ref pos, tokens);
            return state;
        }

        // Identifiers and keywords.
        if (IsIdentStart(c))
        {
            ReadIdentifierOrKeyword(line, ref pos, tokens, Keywords, TypeNames);
            return state;
        }

        // Operators.
        if (IsOperatorChar(c))
        {
            int len = 1;
            if (pos + 2 < line.Length)
            {
                string three = line.Substring(pos, 3);
                if (three is "<<=" or ">>=" or "...")
                    len = 3;
            }
            if (len == 1 && pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||" or
                    "++" or "--" or "+=" or "-=" or "*=" or "/=" or "%=" or
                    "&=" or "|=" or "^=" or "<<" or ">>" or "->" or "##")
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
        if (state.StateId == LexerState.StateInMultiLineComment)
            return ReadBlockComment(line, ref pos, tokens, state);
        return state;
    }

    private static void ReadCharLiteral(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip '

        if (pos < line.Length && line[pos] == '\\')
        {
            pos++; // skip backslash
            if (pos < line.Length) pos++; // skip escaped char
        }
        else if (pos < line.Length)
        {
            pos++; // the character
        }

        if (pos < line.Length && line[pos] == '\'')
            pos++; // closing '

        tokens.Add(new Token(start, pos - start, TokenType.Character));
    }

    private static bool IsLineStart(string line, int pos)
    {
        for (int i = 0; i < pos; i++)
        {
            if (!char.IsWhiteSpace(line[i]))
                return false;
        }
        return true;
    }
}
