namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for SQL.  Handles keywords (case-insensitive), strings (<c>'...'</c>),
/// identifiers, numbers, and comments (<c>--</c> and <c>/* */</c>).
/// </summary>
public sealed class SqlLexer : BaseLexer
{
    public override string LanguageId => "sql";
    public override string[] FileExtensions => [".sql", ".ddl", ".dml"];

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "INSERT", "INTO", "UPDATE", "DELETE",
        "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW", "PROCEDURE",
        "FUNCTION", "TRIGGER", "DATABASE", "SCHEMA", "IF", "ELSE", "BEGIN",
        "END", "DECLARE", "SET", "EXEC", "EXECUTE", "RETURN", "AND", "OR",
        "NOT", "IN", "EXISTS", "BETWEEN", "LIKE", "IS", "NULL", "AS",
        "ON", "JOIN", "INNER", "LEFT", "RIGHT", "OUTER", "FULL", "CROSS",
        "UNION", "ALL", "DISTINCT", "ORDER", "BY", "GROUP", "HAVING",
        "ASC", "DESC", "LIMIT", "OFFSET", "TOP", "WITH", "CASE", "WHEN",
        "THEN", "ELSE", "END", "CAST", "CONVERT", "COALESCE", "NULLIF",
        "VALUES", "DEFAULT", "CONSTRAINT", "PRIMARY", "KEY", "FOREIGN",
        "REFERENCES", "UNIQUE", "CHECK", "NOT", "NULL", "AUTO_INCREMENT",
        "IDENTITY", "GRANT", "REVOKE", "COMMIT", "ROLLBACK", "TRANSACTION",
        "SAVEPOINT", "TRUNCATE", "MERGE", "USING", "MATCHED", "OUTPUT",
        "FETCH", "NEXT", "ROWS", "ONLY", "PERCENT", "OVER", "PARTITION",
        "ROW_NUMBER", "RANK", "DENSE_RANK", "NTILE", "LAG", "LEAD",
        "FIRST_VALUE", "LAST_VALUE", "COUNT", "SUM", "AVG", "MIN", "MAX",
        "TRUE", "FALSE", "GO", "USE", "PRINT", "RAISERROR", "THROW",
        "TRY", "CATCH", "WHILE", "BREAK", "CONTINUE", "CURSOR", "OPEN",
        "CLOSE", "DEALLOCATE", "PIVOT", "UNPIVOT", "EXCEPT", "INTERSECT",
        "ANY", "SOME", "APPLY", "OUTER",
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "INT", "INTEGER", "BIGINT", "SMALLINT", "TINYINT", "BIT",
        "DECIMAL", "NUMERIC", "FLOAT", "REAL", "MONEY", "SMALLMONEY",
        "CHAR", "VARCHAR", "NCHAR", "NVARCHAR", "TEXT", "NTEXT",
        "DATE", "DATETIME", "DATETIME2", "SMALLDATETIME", "TIME",
        "TIMESTAMP", "DATETIMEOFFSET", "UNIQUEIDENTIFIER", "XML",
        "BINARY", "VARBINARY", "IMAGE", "SQL_VARIANT", "ROWVERSION",
        "BOOLEAN", "SERIAL", "BIGSERIAL", "UUID", "JSON", "JSONB",
        "BYTEA", "INTERVAL", "ARRAY", "ENUM", "BLOB", "CLOB",
    };

    protected override LexerState TokenizeNormal(
        string line, ref int pos, List<Token> tokens, LexerState state)
    {
        if (SkipWhitespace(line, ref pos, tokens))
            return state;

        char c = line[pos];

        // Single-line comment: --
        if (c == '-' && pos + 1 < line.Length && line[pos + 1] == '-')
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
        if (c == '\'')
        {
            ReadSqlString(line, ref pos, tokens);
            return state;
        }

        // Quoted identifiers.
        if (c == '"' || c == '[')
        {
            ReadQuotedIdentifier(line, ref pos, tokens);
            return state;
        }

        // Numbers.
        if (char.IsDigit(c) || (c == '.' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
        {
            ReadNumber(line, ref pos, tokens);
            return state;
        }

        // Variables (T-SQL: @var, @@var).
        if (c == '@')
        {
            int start = pos;
            pos++;
            if (pos < line.Length && line[pos] == '@')
                pos++;
            while (pos < line.Length && IsIdentPart(line[pos]))
                pos++;
            tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            return state;
        }

        // Identifiers and keywords.
        if (IsIdentStart(c) || c == '#')
        {
            int len = ReadIdentifierLength(line, pos);
            if (len == 0)
            {
                // # prefix for temp tables.
                int start = pos;
                pos++;
                while (pos < line.Length && IsIdentPart(line[pos]))
                    pos++;
                tokens.Add(new Token(start, pos - start, TokenType.Identifier));
            }
            else
            {
                string word = line.Substring(pos, len);
                TokenType type;
                if (Keywords.Contains(word))
                    type = TokenType.Keyword;
                else if (TypeNames.Contains(word))
                    type = TokenType.TypeName;
                else
                    type = TokenType.Identifier;
                tokens.Add(new Token(pos, len, type));
                pos += len;
            }
            return state;
        }

        // Operators.
        if (c == '=' || c == '<' || c == '>' || c == '!' || c == '+' || c == '-' ||
            c == '*' || c == '/' || c == '%' || c == '&' || c == '|' || c == '^' || c == '~')
        {
            int len = 1;
            if (pos + 1 < line.Length)
            {
                string two = line.Substring(pos, 2);
                if (two is "<>" or "<=" or ">=" or "!=" or "+=" or "-=" or "*=" or "/=" or "%=")
                    len = 2;
            }
            EmitOperator(line, ref pos, tokens, len);
            return state;
        }

        // Punctuation.
        if (c == '(' || c == ')' || c == ',' || c == ';' || c == '.')
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

    private static void ReadSqlString(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        pos++; // skip opening '

        while (pos < line.Length)
        {
            if (line[pos] == '\'' && pos + 1 < line.Length && line[pos + 1] == '\'')
            {
                pos += 2; // escaped quote
            }
            else if (line[pos] == '\'')
            {
                pos++;
                break;
            }
            else
            {
                pos++;
            }
        }

        tokens.Add(new Token(start, pos - start, TokenType.String));
    }

    private static void ReadQuotedIdentifier(string line, ref int pos, List<Token> tokens)
    {
        int start = pos;
        char open = line[pos];
        char close = open == '[' ? ']' : open;
        pos++;

        while (pos < line.Length && line[pos] != close)
            pos++;

        if (pos < line.Length)
            pos++;

        tokens.Add(new Token(start, pos - start, TokenType.Identifier));
    }
}
