using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BayanCompiler.Diagnostics;
using BayanCompiler.Intermediate;
using BayanCompiler.Services;

namespace BayanCompiler.Forms;

/// <summary>
/// Invokes the standalone Bayan compiler or executes in-process when deployed as a single standalone executable.
/// </summary>
internal sealed class CliCompilationClient
{
    private static readonly string[] ArtifactNames =
    {
        "tokens.txt",
        "ast.txt",
        "parse-tree.txt",
        "symbol-table.txt",
        "diagnostics.txt",
        "three-address-code.txt",
        "output.asm"
    };

    /// <summary>
    /// Persists source text, runs the independent CLI or built-in compiler, and returns every generated artifact.
    /// </summary>
    public async Task<CliCompilationRun> CompileAsync(string sourceText)
    {
        string runDirectory = Path.Combine(Path.GetTempPath(), "BayanStudio", "compilations", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        string sourcePath = Path.Combine(runDirectory, "editor-input.bayan");
        string outputPath = Path.Combine(runDirectory, "artifacts");
        await File.WriteAllTextAsync(sourcePath, sourceText, new UTF8Encoding(false));

        string cliDllPath = Path.Combine(AppContext.BaseDirectory, "Bayan.Compiler.Cli.dll");
        string cliExePath = Path.Combine(AppContext.BaseDirectory, "Bayan.Compiler.Cli.exe");

        // When deployed as a standalone single-file EXE or if CLI binaries are not on disk, compile in-process directly
        if (!File.Exists(cliDllPath) && !File.Exists(cliExePath))
        {
            return await CompileInProcessAsync(sourceText, runDirectory, sourcePath, outputPath);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (File.Exists(cliExePath))
            {
                startInfo.FileName = cliExePath;
                startInfo.ArgumentList.Add(sourcePath);
                startInfo.ArgumentList.Add("--out");
                startInfo.ArgumentList.Add(outputPath);
            }
            else
            {
                startInfo.FileName = "dotnet";
                startInfo.ArgumentList.Add(cliDllPath);
                startInfo.ArgumentList.Add(sourcePath);
                startInfo.ArgumentList.Add("--out");
                startInfo.ArgumentList.Add(outputPath);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("تعذر بدء عملية مترجم بيان.");
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string artifactName in ArtifactNames)
            {
                string artifactPath = Path.Combine(outputPath, artifactName);
                artifacts[artifactName] = File.Exists(artifactPath)
                    ? await File.ReadAllTextAsync(artifactPath, Encoding.UTF8)
                    : string.Empty;
            }

            CliCompilationManifest? manifest = null;
            string manifestPath = Path.Combine(outputPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                string manifestJson = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8);
                manifest = JsonSerializer.Deserialize<CliCompilationManifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            return new CliCompilationRun(process.ExitCode, await stdoutTask, await stderrTask, artifacts, manifest, outputPath);
        }
        catch
        {
            // Fallback to built-in compiler if external process execution encounters any environment issue
            return await CompileInProcessAsync(sourceText, runDirectory, sourcePath, outputPath);
        }
    }

    private static async Task<CliCompilationRun> CompileInProcessAsync(string sourceText, string runDirectory, string sourcePath, string outputPath)
    {
        await Task.Yield();
        var utf8 = new UTF8Encoding(false);
        Directory.CreateDirectory(outputPath);

        var result = new OfficialBayanCompilationService().Compile(sourceText, outputPath);

        // Write outputs exactly identical to CLI
        await File.WriteAllTextAsync(Path.Combine(outputPath, "tokens.txt"), TokenPrinter.Format(result.Tokens), utf8);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "ast.txt"), AstPrinter.Format(result.Program), utf8);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "ll1-trace.txt"), result.Ll1Trace, utf8);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "ll1-table.txt"), result.Ll1AnalysisTable, utf8);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "parse-tree.txt"), result.ParseTree, utf8);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "symbol-table.txt"), result.SymbolTable, utf8);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "diagnostics.txt"), string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString())), utf8);

        if (result.IrProgram is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(outputPath, "three-address-code.txt"), IrPrinter.Format(result.IrProgram), utf8);
        }

        if (!string.IsNullOrWhiteSpace(result.AssemblyDisplayCode))
        {
            await File.WriteAllTextAsync(Path.Combine(outputPath, "output.asm"), result.AssemblyDisplayCode, utf8);
        }

        var manifestData = new CliCompilationManifest(
            result.Succeeded,
            result.CompletedStage,
            result.Diagnostics.Count,
            !string.IsNullOrWhiteSpace(result.ExecutablePath),
            result.ExecutablePath ?? string.Empty);

        string manifestJson = JsonSerializer.Serialize(manifestData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(outputPath, "manifest.json"), manifestJson, utf8);

        var artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string artifactName in ArtifactNames)
        {
            string artifactPath = Path.Combine(outputPath, artifactName);
            artifacts[artifactName] = File.Exists(artifactPath)
                ? await File.ReadAllTextAsync(artifactPath, Encoding.UTF8)
                : string.Empty;
        }

        string stdout = $"BAYAN_RESULT success={result.Succeeded.ToString().ToLowerInvariant()} stage={result.CompletedStage} diagnostics={result.Diagnostics.Count}\n" +
                        $"المترجم الداخلي: اكتملت المعالجة بنجاح لمرحلة {result.CompletedStage}.";

        string stderr = result.Diagnostics.Count > 0
            ? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString()))
            : string.Empty;

        return new CliCompilationRun(result.Succeeded ? 0 : 1, stdout, stderr, artifacts, manifestData, outputPath);
    }
}

/// <summary>
/// Represents the manifest emitted by the standalone command-line compiler.
/// </summary>
internal sealed record CliCompilationManifest(
    bool Success,
    string CompletedStage,
    int DiagnosticCount,
    bool WindowsExecutableAvailable,
    string WindowsExecutablePath);

/// <summary>
/// Represents one completed external compiler invocation and its persisted outputs.
/// </summary>
internal sealed record CliCompilationRun(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    IReadOnlyDictionary<string, string> Artifacts,
    CliCompilationManifest? Manifest,
    string OutputDirectory)
{
    /// <summary>
    /// Gets a persisted compiler artifact or an empty string when the stage did not produce it.
    /// </summary>
    public string GetArtifact(string name)
    {
        return Artifacts.TryGetValue(name, out string? value) ? value : string.Empty;
    }
}
