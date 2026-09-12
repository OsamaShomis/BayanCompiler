namespace BayanCompiler.Semantics;

/// <summary>
/// Represents the full semantic shape of a primitive, list, record, or procedure type.
/// </summary>
public sealed record TypeDescriptor(
    LanguageType Kind,
    string? Name = null,
    TypeDescriptor? ElementType = null,
    int? ListLength = null,
    IReadOnlyDictionary<string, TypeDescriptor>? Fields = null)
{
    /// <summary>
    /// Creates a descriptor for a primitive or simple language category.
    /// </summary>
    public static TypeDescriptor Primitive(LanguageType kind) => new(kind);

    /// <summary>
    /// Creates a descriptor for a fixed-size official list type.
    /// </summary>
    public static TypeDescriptor List(TypeDescriptor elementType, int length) => new(LanguageType.List, null, elementType, length);

    /// <summary>
    /// Creates a descriptor for a record type with immutable field lookup entries.
    /// </summary>
    public static TypeDescriptor Record(string? name, IReadOnlyDictionary<string, TypeDescriptor> fields) => new(LanguageType.Record, name, null, null, fields);
}
