using BayanCompiler.Frontend;
using BayanCompiler.Lexing;

namespace BayanCompiler.Syntax;

/// <summary>
/// يمثل قيمة ثابتة مثل عدد أو نص أو قيمة منطقية.
/// </summary>
public sealed record LiteralNode(
    TokenType LiteralType,
    string Value,
    SourceSpan Span) : ExpressionNode(Span);

/// <summary>
/// يمثل استخدام اسم متغير مثل: س أو مجموع.
/// </summary>
public sealed record IdentifierNode(
    string Name,
    SourceSpan Span) : ExpressionNode(Span);

/// <summary>
/// يمثل عملية ثنائية مثل: 2 + 3 أو س * 5.
/// </summary>
public sealed record BinaryExpressionNode(
    ExpressionNode Left,
    TokenType Operator,
    ExpressionNode Right,
    SourceSpan Span) : ExpressionNode(Span);

/// <summary>
/// يمثل تعبيراً موضوعاً بين قوسين مثل: (س + 1).
/// </summary>
public sealed record GroupingNode(
    ExpressionNode Expression,
    SourceSpan Span) : ExpressionNode(Span);

/// <summary>
/// يمثل عملية أحادية مثل !صحيح أو -5.
/// </summary>
public sealed record UnaryExpressionNode(
    TokenType Operator,
    ExpressionNode Operand,
    SourceSpan Span) : ExpressionNode(Span);
