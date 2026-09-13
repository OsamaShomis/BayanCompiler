using BayanCompiler.Frontend;

namespace BayanCompiler.Syntax;

/// <summary>
/// يمثل العقدة الأساسية لكل عنصر في شجرة البرنامج المجردة AST.
/// </summary>
public abstract record AstNode(SourceSpan Span);

/// <summary>
/// يمثل عقدة أساسية لكل تعليمة، مثل التعريف والطباعة.
/// </summary>
public abstract record StatementNode(SourceSpan Span) : AstNode(Span);

/// <summary>
/// يمثل عقدة أساسية لكل تعبير، مثل العدد أو الاسم أو العملية الحسابية.
/// </summary>
public abstract record ExpressionNode(SourceSpan Span) : AstNode(Span);
