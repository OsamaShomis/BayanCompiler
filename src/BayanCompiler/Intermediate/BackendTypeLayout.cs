using BayanCompiler.Semantics;

namespace BayanCompiler.Intermediate;

/// <summary>
/// Describes the word-level MIPS memory layout of a backend-supported official type.
/// </summary>
internal sealed record BackendTypeLayout(
    LanguageType Kind,
    int WordCount,
    BackendTypeLayout? ElementType = null,
    IReadOnlyDictionary<string, BackendFieldLayout>? Fields = null);

/// <summary>
/// Describes one record field by its zero-based word offset and full structural layout.
/// </summary>
internal sealed record BackendFieldLayout(int WordOffset, BackendTypeLayout Layout);
