namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for TypeScript.  Extends <see cref="JavaScriptLexer"/> with
/// additional type-system keywords such as <c>interface</c>, <c>type</c>,
/// <c>enum</c>, <c>as</c>, <c>is</c>, <c>keyof</c>, etc.
/// </summary>
public sealed class TypeScriptLexer : JavaScriptLexer
{
    public override string LanguageId => "typescript";
    public override string[] FileExtensions => [".ts", ".tsx", ".mts", ".cts"];

    private static readonly HashSet<string> TsExtraKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "asserts", "declare", "enum", "implements",
        "infer", "interface", "is", "keyof", "module", "namespace",
        "never", "override", "private", "protected", "public",
        "readonly", "satisfies", "type", "unknown", "using",
    };

    protected override HashSet<string>? ExtraKeywords => TsExtraKeywords;
}
