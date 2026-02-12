namespace Bascanka.Core.Navigation;

/// <summary>
/// Enumerates the kinds of code symbols that can be discovered by the
/// <see cref="SymbolParser"/>.
/// </summary>
public enum SymbolKind
{
    /// <summary>A class declaration.</summary>
    Class,

    /// <summary>A method declaration.</summary>
    Method,

    /// <summary>A standalone function (non-member).</summary>
    Function,

    /// <summary>A property declaration.</summary>
    Property,

    /// <summary>An interface declaration.</summary>
    Interface,

    /// <summary>An enum declaration.</summary>
    Enum,

    /// <summary>A struct declaration.</summary>
    Struct,
}
