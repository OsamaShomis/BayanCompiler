using BayanCompiler.Frontend;
using BayanCompiler.Lexing;

namespace BayanCompiler.Syntax;

/// <summary>
/// Represents a top-level declaration defined by the doctor specification.
/// </summary>
public abstract record DeclarationNode(SourceSpan Span) : AstNode(Span);

/// <summary>
/// Represents a constant declaration that becomes read-only after semantic analysis.
/// </summary>
public sealed record ConstantDeclarationNode(
    string Name,
    TypeSyntaxNode? ExplicitType,
    ExpressionNode Value,
    SourceSpan Span) : DeclarationNode(Span);

/// <summary>
/// Represents a user-defined named type declaration.
/// </summary>
public sealed record TypeDeclarationNode(
    string Name,
    TypeSyntaxNode Definition,
    SourceSpan Span) : DeclarationNode(Span);

/// <summary>
/// Represents a variable group declared with a shared type and optional initializer.
/// </summary>
public sealed record VariableDeclarationGroupNode(
    IReadOnlyList<string> Names,
    TypeSyntaxNode Type,
    ExpressionNode? Initializer,
    SourceSpan Span) : DeclarationNode(Span);

/// <summary>
/// Represents a procedure declaration with formal parameters and a local body.
/// </summary>
public sealed record ProcedureDeclarationNode(
    string Name,
    IReadOnlyList<ParameterNode> Parameters,
    BlockNode Body,
    SourceSpan Span) : DeclarationNode(Span);

/// <summary>
/// Represents a procedure parameter passed by value or by reference.
/// </summary>
public sealed record ParameterNode(
    string Name,
    TypeSyntaxNode Type,
    ParameterPassingMode PassingMode,
    SourceSpan Span) : AstNode(Span);

/// <summary>
/// Identifies how a procedure parameter is supplied by a caller.
/// </summary>
public enum ParameterPassingMode
{
    ByValue,
    ByReference
}

/// <summary>
/// Represents a type expression written in source code.
/// </summary>
public abstract record TypeSyntaxNode(SourceSpan Span) : AstNode(Span);

/// <summary>
/// Represents a built-in type or a reference to a user-defined type name.
/// </summary>
public sealed record NamedTypeSyntaxNode(
    string Name,
    TokenType TypeToken,
    SourceSpan Span) : TypeSyntaxNode(Span);

/// <summary>
/// Represents a fixed-size list type whose size is an integer expression.
/// </summary>
public sealed record ListTypeSyntaxNode(
    ExpressionNode Size,
    TypeSyntaxNode ElementType,
    SourceSpan Span) : TypeSyntaxNode(Span);

/// <summary>
/// Represents a record type composed of named fields.
/// </summary>
public sealed record RecordTypeSyntaxNode(
    IReadOnlyList<FieldDeclarationNode> Fields,
    SourceSpan Span) : TypeSyntaxNode(Span);

/// <summary>
/// Represents one field inside a record definition.
/// </summary>
public sealed record FieldDeclarationNode(
    string Name,
    TypeSyntaxNode Type,
    SourceSpan Span) : AstNode(Span);

/// <summary>
/// Represents an input command whose target must be validated as an assignable location.
/// </summary>
public sealed record ReadNode(
    ExpressionNode Target,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// Represents an output command containing one or more expressions.
/// </summary>
public sealed record PrintListNode(
    IReadOnlyList<ExpressionNode> Expressions,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// Represents a counted repeat loop with an optional positive or negative step expression.
/// </summary>
public sealed record RepeatToNode(
    string IteratorName,
    ExpressionNode Start,
    ExpressionNode End,
    ExpressionNode? Step,
    BlockNode Body,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// Represents a repeat-until loop that evaluates its condition after the body.
/// </summary>
public sealed record RepeatUntilNode(
    BlockNode Body,
    ExpressionNode Condition,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// Represents an assignment whose target can be an indexed element or record field.
/// </summary>
public sealed record ComplexAssignmentNode(
    ExpressionNode Target,
    ExpressionNode Expression,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// Represents an expression used as a statement, primarily a procedure call.
/// </summary>
public sealed record ExpressionStatementNode(
    ExpressionNode Expression,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// Represents a procedure call and its actual argument list.
/// </summary>
public sealed record CallExpressionNode(
    string ProcedureName,
    IReadOnlyList<ExpressionNode> Arguments,
    SourceSpan Span) : ExpressionNode(Span);

/// <summary>
/// Represents access to a fixed-size list element through an index expression.
/// </summary>
public sealed record IndexedAccessNode(
    ExpressionNode Collection,
    ExpressionNode Index,
    SourceSpan Span) : ExpressionNode(Span);

/// <summary>
/// Represents access to a named record field.
/// </summary>
public sealed record FieldAccessNode(
    ExpressionNode Record,
    string FieldName,
    SourceSpan Span) : ExpressionNode(Span);
