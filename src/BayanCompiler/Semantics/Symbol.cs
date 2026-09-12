using BayanCompiler.Frontend;
using BayanCompiler.Syntax;

namespace BayanCompiler.Semantics;

/// <summary>
/// يمثل متغيراً معرفاً في البرنامج مع نوعه وموقع تعريفه.
/// </summary>
public sealed record Symbol(
    string Name,
    LanguageType Type,
    SourceSpan DeclarationSpan)
{
    /// <summary>
    /// Gets the declaration category required for semantic checks and symbol-table output.
    /// </summary>
    public SymbolKind Kind { get; init; } = SymbolKind.Variable;

    /// <summary>
    /// Gets whether an assignment to this symbol must be rejected.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Gets the formal parameter signature when this symbol represents a procedure.
    /// </summary>
    public IReadOnlyList<ProcedureParameterSignature> Parameters { get; init; } = Array.Empty<ProcedureParameterSignature>();

    /// <summary>
    /// Gets the structural type information needed for list indexing and record field access.
    /// </summary>
    public TypeDescriptor? DetailedType { get; init; }
}

/// <summary>
/// Describes one procedure parameter for call-site semantic validation.
/// </summary>
public sealed record ProcedureParameterSignature(string Name, LanguageType Type, ParameterPassingMode PassingMode)
{
    /// <summary>
    /// Gets the structural parameter type when the formal parameter is a list, record, or named type.
    /// </summary>
    public TypeDescriptor? DetailedType { get; init; }
}

/// <summary>
/// Identifies the source declaration category represented by a symbol-table entry.
/// </summary>
public enum SymbolKind
{
    Variable,
    Constant,
    Type,
    Procedure,
    Parameter
}
