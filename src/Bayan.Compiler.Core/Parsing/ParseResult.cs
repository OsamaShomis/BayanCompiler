using BayanCompiler.Frontend;
using BayanCompiler.Syntax;

namespace BayanCompiler.Parsing;

/// <summary>
/// يجمع برنامج AST والتشخيصات النحوية.
/// </summary>
public sealed record ParseResult(ProgramNode Program, IReadOnlyList<Diagnostic> Diagnostics);
