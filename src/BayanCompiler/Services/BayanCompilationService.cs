using BayanCompiler.CodeGeneration;
using BayanCompiler.Diagnostics;
using BayanCompiler.Frontend;
using BayanCompiler.Intermediate;
using BayanCompiler.Lexing;
using BayanCompiler.Parsing;
using BayanCompiler.Semantics;
using BayanCompiler.Syntax;

namespace BayanCompiler.Services;

/// <summary>
/// Runs the existing compiler stages through one reusable service boundary.
/// Both the standalone CLI and the future editor integration use this class.
/// </summary>
public sealed class BayanCompilationService
{
    public BayanCompilationResult Compile(string sourceText, string outputDirectory)
    {
        var diagnostics = new List<Diagnostic>();

        var lexResult = new Lexer(sourceText).Lex();
        diagnostics.AddRange(lexResult.Diagnostics);
        if (diagnostics.Count > 0)
        {
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, diagnostics, "Lexer");
        }

        var parser = new Ll1Parser(lexResult.Tokens);
        ParseResult parseResult = parser.Parse();
        diagnostics.AddRange(parseResult.Diagnostics);
        if (diagnostics.Count > 0)
        {
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, parseResult.Program, diagnostics, "LL(1)", parser.FormatTrace());
        }

        var semanticResult = new SemanticAnalyzer().Analyze(parseResult.Program);
        diagnostics.AddRange(semanticResult.Diagnostics);
        if (diagnostics.Count > 0)
        {
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, parseResult.Program, diagnostics, "Semantic", parser.FormatTrace());
        }

        try
        {
            IrProgram irProgram = new IrGenerator().Generate(parseResult.Program);
            var asmResult = new AssemblyGenerator().Generate(irProgram, outputDirectory);

            return new BayanCompilationResult(
                lexResult.Tokens,
                parseResult.Program,
                diagnostics,
                irProgram,
                asmResult.AssemblyDisplayCode,
                asmResult.ExecutablePath,
                "Assembly",
                parser.FormatTrace());
        }
        catch (Exception exception)
        {
            diagnostics.Add(new Diagnostic("ASM001", exception.Message, new SourceSpan(0, 0, 1, 1)));
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, parseResult.Program, diagnostics, "Assembly", parser.FormatTrace());
        }
    }
}

public sealed record BayanCompilationResult(
    IReadOnlyList<Token> Tokens,
    ProgramNode Program,
    IReadOnlyList<Diagnostic> Diagnostics,
    IrProgram? IrProgram,
    string? AssemblyDisplayCode,
    string? ExecutablePath,
    string CompletedStage,
    string Ll1Trace)
{
    public string ParseTree { get; init; } = string.Empty;
    public string Ll1AnalysisTable { get; init; } = string.Empty;
    public string SymbolTable { get; init; } = string.Empty;

    public bool Succeeded => Diagnostics.Count == 0 && !string.IsNullOrWhiteSpace(ExecutablePath);

    public static BayanCompilationResult FromStoppedPipeline(
        IReadOnlyList<Token> tokens,
        IReadOnlyList<Diagnostic> diagnostics,
        string completedStage)
    {
        return new BayanCompilationResult(
            tokens,
            new ProgramNode("", new List<StatementNode>(), new SourceSpan(0, 0, 1, 1)),
            diagnostics,
            null,
            null,
            null,
            completedStage,
            string.Empty);
    }

    public static BayanCompilationResult FromStoppedPipeline(
        IReadOnlyList<Token> tokens,
        ProgramNode program,
        IReadOnlyList<Diagnostic> diagnostics,
        string completedStage,
        string ll1Trace)
    {
        return new BayanCompilationResult(tokens, program, diagnostics, null, null, null, completedStage, ll1Trace);
    }
}
