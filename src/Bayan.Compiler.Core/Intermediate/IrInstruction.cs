using BayanCompiler.Frontend;
using BayanCompiler.Lexing;

namespace BayanCompiler.Intermediate;

/// <summary>
/// يمثل تعليمة واحدة في Three-Address Code قبل تحويلها إلى MIPS.
/// </summary>
public sealed record IrInstruction(
    IrOpcode Opcode,
    IrValue? Result,
    IrValue? Left,
    IrValue? Right,
    TokenType? Operator,
    string? TargetLabel,
    SourceSpan Span);
