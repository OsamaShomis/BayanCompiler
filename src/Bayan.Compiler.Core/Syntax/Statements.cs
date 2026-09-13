using BayanCompiler.Frontend;

namespace BayanCompiler.Syntax;

/// <summary>
/// يمثل ملف برنامج كامل يبدأ بكلمة برنامج وينتهي بكلمة نهاية.
/// </summary>
public sealed record ProgramNode(
    string Name,
    IReadOnlyList<StatementNode> Statements,
    SourceSpan Span) : AstNode(Span)
{
    /// <summary>
    /// Gets the top-level declarations written before the executable statement block.
    /// </summary>
    public IReadOnlyList<DeclarationNode> Declarations { get; init; } = Array.Empty<DeclarationNode>();

    /// <summary>
    /// Initializes a program with official top-level declarations and executable statements.
    /// </summary>
    public ProgramNode(
        string name,
        IReadOnlyList<DeclarationNode> declarations,
        IReadOnlyList<StatementNode> statements,
        SourceSpan span) : this(name, statements, span)
    {
        Declarations = declarations;
    }
}

/// <summary>
/// يمثل تعريف متغير مثل: دع س : عدد = 5؛
/// </summary>
public sealed record VarDeclarationNode(
    string Name,
    string TypeName,
    ExpressionNode? Initializer,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// يمثل تعليمة طباعة مثل: اطبع(س)؛
/// </summary>
public sealed record PrintNode(
    ExpressionNode Expression,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// يمثل إسناد قيمة إلى متغير معرّف مسبقاً مثل: س ← س + 1؛
/// </summary>
public sealed record AssignmentNode(
    string Name,
    ExpressionNode Expression,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// يمثل مجموعة تعليمات محاطة بقوسين معقوفين.
/// </summary>
public sealed record BlockNode(
    IReadOnlyList<StatementNode> Statements,
    SourceSpan Span) : StatementNode(Span)
{
    /// <summary>
    /// Gets declarations scoped to this executable block, such as procedure-local variables.
    /// </summary>
    public IReadOnlyList<DeclarationNode> Declarations { get; init; } = Array.Empty<DeclarationNode>();

    /// <summary>
    /// Initializes an executable block with local declarations and statements.
    /// </summary>
    public BlockNode(
        IReadOnlyList<DeclarationNode> declarations,
        IReadOnlyList<StatementNode> statements,
        SourceSpan span) : this(statements, span)
    {
        Declarations = declarations;
    }
}

/// <summary>
/// يمثل شرط إذا مع فرع وإلا اختياري.
/// </summary>
public sealed record IfNode(
    ExpressionNode Condition,
    BlockNode ThenBlock,
    BlockNode? ElseBlock,
    SourceSpan Span) : StatementNode(Span);

/// <summary>
/// يمثل حلقة طالما التي تنفذ كتلتها ما دام شرطها صحيحاً.
/// </summary>
public sealed record WhileNode(
    ExpressionNode Condition,
    BlockNode Body,
    SourceSpan Span) : StatementNode(Span);
