using System.Text.RegularExpressions;
using Bascanka.Core.Buffer;

namespace Bascanka.Core.Navigation;

/// <summary>
/// A lightweight, regex-based symbol extractor that identifies common language
/// constructs (classes, methods, functions, properties, interfaces, enums) in
/// source code.  This is not a full parser; it uses heuristic regex patterns
/// tuned for each supported language.
/// </summary>
public static class SymbolParser
{
    /// <summary>
    /// Maps a language identifier to its set of symbol-extraction patterns.
    /// Language IDs follow VS Code / TextMate conventions (e.g. "csharp",
    /// "javascript", "python", "java", "go", "rust", "typescript", "cpp").
    /// </summary>
    private static readonly Dictionary<string, LanguagePatterns> _languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Class,     @"(?:public|private|protected|internal)?\s*(?:static\s+|abstract\s+|sealed\s+|partial\s+)*class\s+(\w+)"),
            new SymbolPattern(SymbolKind.Interface,  @"(?:public|private|protected|internal)?\s*(?:partial\s+)?interface\s+(\w+)"),
            new SymbolPattern(SymbolKind.Enum,       @"(?:public|private|protected|internal)?\s*enum\s+(\w+)"),
            new SymbolPattern(SymbolKind.Method,     @"(?:public|private|protected|internal)?\s*(?:static\s+|virtual\s+|override\s+|abstract\s+|async\s+)*[\w<>\[\],\s]+?\s+(\w+)\s*\("),
            new SymbolPattern(SymbolKind.Property,   @"(?:public|private|protected|internal)?\s*(?:static\s+|virtual\s+|override\s+|abstract\s+)*[\w<>\[\],\?\s]+?\s+(\w+)\s*\{\s*(?:get|set|init)"),
        ]),

        ["java"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Class,     @"(?:public|private|protected)?\s*(?:static\s+|abstract\s+|final\s+)*class\s+(\w+)"),
            new SymbolPattern(SymbolKind.Interface,  @"(?:public|private|protected)?\s*interface\s+(\w+)"),
            new SymbolPattern(SymbolKind.Enum,       @"(?:public|private|protected)?\s*enum\s+(\w+)"),
            new SymbolPattern(SymbolKind.Method,     @"(?:public|private|protected)?\s*(?:static\s+|final\s+|synchronized\s+|abstract\s+)*[\w<>\[\],\s]+?\s+(\w+)\s*\("),
        ]),

        ["javascript"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Class,     @"\bclass\s+(\w+)"),
            new SymbolPattern(SymbolKind.Method,     @"\bfunction\s+(\w+)\s*\("),
            new SymbolPattern(SymbolKind.Method,     @"\b(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\("),
            new SymbolPattern(SymbolKind.Method,     @"\b(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?function"),
            new SymbolPattern(SymbolKind.Method,     @"(?:async\s+)?(\w+)\s*\([^)]*\)\s*\{"),
        ]),

        ["typescript"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Class,     @"\bclass\s+(\w+)"),
            new SymbolPattern(SymbolKind.Interface,  @"\binterface\s+(\w+)"),
            new SymbolPattern(SymbolKind.Enum,       @"\benum\s+(\w+)"),
            new SymbolPattern(SymbolKind.Method,     @"\bfunction\s+(\w+)\s*[<(]"),
            new SymbolPattern(SymbolKind.Method,     @"\b(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\("),
            new SymbolPattern(SymbolKind.Method,     @"(?:public|private|protected)?\s*(?:static\s+|async\s+)*(\w+)\s*\([^)]*\)\s*[:{]"),
        ]),

        ["python"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Class,     @"^class\s+(\w+)\s*[:(]", IsLineStart: true),
            new SymbolPattern(SymbolKind.Method,     @"^(?:async\s+)?def\s+(\w+)\s*\(", IsLineStart: true),
        ]),

        ["go"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Method,     @"\bfunc\s+(\w+)\s*\("),
            new SymbolPattern(SymbolKind.Method,     @"\bfunc\s+\([^)]+\)\s+(\w+)\s*\("),
            new SymbolPattern(SymbolKind.Interface,  @"\btype\s+(\w+)\s+interface\b"),
            new SymbolPattern(SymbolKind.Struct,     @"\btype\s+(\w+)\s+struct\b"),
        ]),

        ["rust"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Method,     @"\bfn\s+(\w+)\s*[<(]"),
            new SymbolPattern(SymbolKind.Struct,     @"\bstruct\s+(\w+)"),
            new SymbolPattern(SymbolKind.Enum,       @"\benum\s+(\w+)"),
            new SymbolPattern(SymbolKind.Interface,  @"\btrait\s+(\w+)"),
        ]),

        ["cpp"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Class,     @"\bclass\s+(\w+)"),
            new SymbolPattern(SymbolKind.Struct,     @"\bstruct\s+(\w+)"),
            new SymbolPattern(SymbolKind.Enum,       @"\benum\s+(?:class\s+)?(\w+)"),
            new SymbolPattern(SymbolKind.Method,     @"[\w:*&<>\s]+\s+(\w+)\s*\([^;]*$"),
        ]),

        ["c"] = new LanguagePatterns(
        [
            new SymbolPattern(SymbolKind.Struct,     @"\bstruct\s+(\w+)"),
            new SymbolPattern(SymbolKind.Enum,       @"\benum\s+(\w+)"),
            new SymbolPattern(SymbolKind.Method,     @"[\w*\s]+\s+(\w+)\s*\([^;]*$"),
        ]),
    };

    /// <summary>
    /// Parses the content of <paramref name="buffer"/> for symbols using
    /// regex patterns appropriate for <paramref name="languageId"/>.
    /// </summary>
    /// <param name="buffer">The text buffer to scan.</param>
    /// <param name="languageId">
    /// A language identifier (e.g. "csharp", "javascript", "python").
    /// Case-insensitive.
    /// </param>
    /// <returns>A list of discovered symbols, ordered by line number.</returns>
    public static List<SymbolInfo> Parse(PieceTable buffer, string languageId)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(languageId);

        if (!_languages.TryGetValue(languageId, out LanguagePatterns? lang))
            return [];

        long docLength = buffer.Length;
        if (docLength == 0)
            return [];

        // Extract the full text.  For very large files this could be chunked,
        // but symbol parsing is typically run on demand and the regex engine
        // handles large strings efficiently.
        string text = buffer.GetText(0, docLength);

        var results = new List<SymbolInfo>();
        var seen = new HashSet<(string name, long line)>();

        foreach (SymbolPattern sp in lang.Patterns)
        {
            RegexOptions opts = RegexOptions.Compiled;
            if (sp.IsLineStart)
                opts |= RegexOptions.Multiline;

            Regex regex;
            try
            {
                regex = new Regex(sp.Pattern, opts, TimeSpan.FromSeconds(5));
            }
            catch (RegexParseException)
            {
                continue;
            }

            MatchCollection matches = regex.Matches(text);
            foreach (Match m in matches)
            {
                if (m.Groups.Count < 2 || !m.Groups[1].Success)
                    continue;

                string name = m.Groups[1].Value;

                // Skip common false positives (keywords that look like identifiers).
                if (IsKeyword(name))
                    continue;

                // Compute 1-based line number from character offset.
                long lineNumber = ComputeLineNumber(text, m.Index);

                // Avoid duplicate entries on the same line.
                if (!seen.Add((name, lineNumber)))
                    continue;

                results.Add(new SymbolInfo
                {
                    Name = name,
                    Kind = sp.Kind,
                    LineNumber = lineNumber,
                    Offset = m.Index,
                });
            }
        }

        results.Sort((a, b) => a.LineNumber.CompareTo(b.LineNumber));
        return results;
    }

    /// <summary>
    /// Computes a 1-based line number from a character offset by counting
    /// newline characters before it.
    /// </summary>
    private static long ComputeLineNumber(string text, int charOffset)
    {
        long line = 1;
        for (int i = 0; i < charOffset && i < text.Length; i++)
        {
            if (text[i] == '\n')
                line++;
        }
        return line;
    }

    /// <summary>
    /// Filters out common language keywords that regex patterns may
    /// accidentally capture as symbol names.
    /// </summary>
    private static bool IsKeyword(string name)
    {
        // A small universal set of keywords that are never valid symbol names.
        return name switch
        {
            "if" or "else" or "for" or "while" or "do" or "switch" or "case" or
            "return" or "break" or "continue" or "new" or "try" or "catch" or
            "finally" or "throw" or "throws" or "void" or "null" or "true" or
            "false" or "this" or "base" or "super" or "import" or "using" or
            "namespace" or "package" or "var" or "let" or "const" or "static" or
            "public" or "private" or "protected" or "internal" or "abstract" or
            "virtual" or "override" or "sealed" or "final" or "async" or "await" or
            "yield" or "typeof" or "sizeof" or "default" or "in" or "out" or
            "ref" or "params" or "string" or "int" or "long" or "bool" or
            "float" or "double" or "decimal" or "char" or "byte" or "object"
                => true,
            _ => false,
        };
    }

    /// <summary>
    /// Holds the regex patterns for a single language.
    /// </summary>
    private sealed record LanguagePatterns(SymbolPattern[] Patterns);

    /// <summary>
    /// A single regex pattern that extracts a named symbol of a specific kind.
    /// Group 1 of the regex must capture the symbol name.
    /// </summary>
    private sealed record SymbolPattern(SymbolKind Kind, string Pattern, bool IsLineStart = false);
}
