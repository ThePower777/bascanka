namespace Bascanka.Core.Syntax.Lexers;

/// <summary>
/// Lexer for C++.  Extends <see cref="CLexer"/> with additional C++ keywords
/// such as <c>class</c>, <c>namespace</c>, <c>template</c>, <c>auto</c>,
/// <c>constexpr</c>, <c>nullptr</c>, etc.
/// </summary>
public sealed class CppLexer : CLexer
{
    public override string LanguageId => "cpp";
    public override string[] FileExtensions => [".cpp", ".cxx", ".cc", ".c++", ".hpp", ".hxx", ".hh", ".h++", ".ipp"];

    private static readonly HashSet<string> CppKeywords = new(StringComparer.Ordinal)
    {
        // All C keywords.
        "auto", "break", "case", "char", "const", "continue", "default",
        "do", "double", "else", "enum", "extern", "float", "for", "goto",
        "if", "inline", "int", "long", "register", "restrict", "return",
        "short", "signed", "sizeof", "static", "struct", "switch",
        "typedef", "union", "unsigned", "void", "volatile", "while",
        "true", "false", "NULL",

        // C++ additions.
        "alignas", "alignof", "and", "and_eq", "asm", "bitand", "bitor",
        "bool", "catch", "char8_t", "char16_t", "char32_t", "class",
        "co_await", "co_return", "co_yield", "compl", "concept",
        "const_cast", "consteval", "constexpr", "constinit", "decltype",
        "delete", "dynamic_cast", "explicit", "export", "friend",
        "mutable", "namespace", "new", "noexcept", "not", "not_eq",
        "nullptr", "operator", "or", "or_eq", "override", "private",
        "protected", "public", "reinterpret_cast", "requires",
        "static_assert", "static_cast", "template", "this", "thread_local",
        "throw", "try", "typeid", "typename", "using", "virtual",
        "wchar_t", "xor", "xor_eq", "final", "import", "module",
    };

    private static readonly HashSet<string> CppTypeNames = new(StringComparer.Ordinal)
    {
        "size_t", "ssize_t", "ptrdiff_t", "intptr_t", "uintptr_t",
        "int8_t", "int16_t", "int32_t", "int64_t",
        "uint8_t", "uint16_t", "uint32_t", "uint64_t",
        "string", "wstring", "string_view", "wstring_view",
        "vector", "map", "set", "unordered_map", "unordered_set",
        "list", "deque", "array", "pair", "tuple", "optional",
        "variant", "any", "span", "shared_ptr", "unique_ptr", "weak_ptr",
        "function", "thread", "mutex", "future", "promise",
        "istream", "ostream", "iostream", "ifstream", "ofstream",
        "stringstream", "ostringstream", "istringstream",
    };

    protected override HashSet<string> Keywords => CppKeywords;
    protected override HashSet<string> TypeNames => CppTypeNames;
}
