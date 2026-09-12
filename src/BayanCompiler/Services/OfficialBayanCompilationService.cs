using BayanCompiler.Frontend;
using BayanCompiler.CodeGeneration;
using BayanCompiler.Diagnostics;
using BayanCompiler.Intermediate;
using BayanCompiler.Lexing;
using BayanCompiler.Parsing;
using BayanCompiler.Semantics;
using BayanCompiler.Syntax;

namespace BayanCompiler.Services;

/// <summary>
/// Runs the official doctor-language front end through lexer, LL(1), AST lowering, and semantic analysis.
/// </summary>
public sealed class OfficialBayanCompilationService
{
    /// <summary>
    /// Compiles official source through the completed front-end stages and preserves all real artifacts.
    /// </summary>
    public BayanCompilationResult Compile(string sourceText, string outputDirectory)
    {
        LexResult lexResult = new Lexer(sourceText).Lex();
        if (lexResult.Diagnostics.Count > 0)
        {
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, lexResult.Diagnostics, "Lexer") with
            {
                Ll1AnalysisTable = DoctorLl1Parser.FormatAnalysisTable()
            };
        }

        var parser = new DoctorLl1Parser(lexResult.Tokens);
        DoctorParseResult parseResult = parser.Parse();
        string parseTree = DoctorLl1Parser.FormatParseTree(parseResult.ParseTree);
        string analysisTable = DoctorLl1Parser.FormatAnalysisTable();
        if (!parseResult.Succeeded || parseResult.Program is null)
        {
            ProgramNode fallback = new("", Array.Empty<StatementNode>(), new SourceSpan(0, 0, 1, 1));
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, fallback, parseResult.Diagnostics, "LL(1)", parser.FormatTrace()) with
            {
                ParseTree = parseTree,
                Ll1AnalysisTable = analysisTable
            };
        }

        DoctorSemanticResult semanticResult = new DoctorSemanticAnalyzer().Analyze(parseResult.Program);
        if (!semanticResult.Succeeded)
        {
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, parseResult.Program, semanticResult.Diagnostics, "Semantic", parser.FormatTrace()) with
            {
                ParseTree = parseTree,
                Ll1AnalysisTable = analysisTable,
                SymbolTable = semanticResult.SymbolTable
            };
        }

        try
        {
            IrProgram irProgram = new IrGenerator().Generate(parseResult.Program);
            var asmResult = new AssemblyGenerator().Generate(irProgram, outputDirectory);

            return new BayanCompilationResult(
                lexResult.Tokens,
                parseResult.Program,
                Array.Empty<Diagnostic>(),
                irProgram,
                asmResult.AssemblyDisplayCode,
                asmResult.ExecutablePath,
                "Assembly",
                parser.FormatTrace())
            {
                ParseTree = parseTree,
                Ll1AnalysisTable = analysisTable,
                SymbolTable = semanticResult.SymbolTable
            };
        }
        catch (Exception exception)
        {
            var diagnostics = new[] { new Diagnostic("ASM001", exception.Message, new SourceSpan(0, 0, 1, 1)) };
            return BayanCompilationResult.FromStoppedPipeline(lexResult.Tokens, parseResult.Program, diagnostics, "Assembly", parser.FormatTrace()) with
            {
                ParseTree = parseTree,
                Ll1AnalysisTable = analysisTable,
                SymbolTable = semanticResult.SymbolTable
            };
        }
    }
}

