namespace Bascanka.Core.Navigation;

/// <summary>
/// Describes a single code symbol (class, method, property, etc.) discovered
/// in a source document, including its name, kind, and location.
/// </summary>
public sealed class SymbolInfo
{
    /// <summary>
    /// The unqualified name of the symbol (e.g. "MyClass", "DoWork").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The category of the symbol.
    /// </summary>
    public required SymbolKind Kind { get; init; }

    /// <summary>
    /// One-based line number where the symbol is declared.
    /// </summary>
    public long LineNumber { get; init; }

    /// <summary>
    /// Zero-based character offset into the document where the symbol starts.
    /// </summary>
    public long Offset { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} ({Kind}, line {LineNumber})";
}
