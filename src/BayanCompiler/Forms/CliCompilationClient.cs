using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BayanCompiler.Forms;

/// <summary>
/// Invokes the standalone Bayan compiler and loads the artifacts it writes for the editor.
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
    /// Persists source text, runs the independent CLI, and returns every generated artifact.
    /// </summary>
    public async Task<CliCompilationRun> CompileAsync(string sourceText)
    {
        string runDirectory = Path.Combine(Path.GetTempPath(), "BayanStudio", "compilations", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        string sourcePath = Path.Combine(runDirectory, "editor-input.bayan");
        string outputPath = Path.Combine(runDirectory, "artifacts");
        await File.WriteAllTextAsync(sourcePath, sourceText, new UTF8Encoding(false));

        string cliBinary = ResolveCliBinary();
        bool isDirectExe = cliBinary.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = isDirectExe ? cliBinary : "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!isDirectExe)
        {
            startInfo.ArgumentList.Add(cliBinary);
        }

        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("--out");
        startInfo.ArgumentList.Add(outputPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("تعذر بدء مترجم بيان المستقل.");
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

    private static string ResolveCliBinary()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Bayan.Compiler.Cli.exe"),
            Path.Combine(AppContext.BaseDirectory, "Bayan.Compiler.Cli.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "Bayan.Compiler.Cli", "Bayan.Compiler.Cli.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "Bayan.Compiler.Cli", "Bayan.Compiler.Cli.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "publish", "Bayan.Compiler.Cli", "Bayan.Compiler.Cli.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Bayan.Compiler.Cli", "bin", "Debug", "net9.0", "Bayan.Compiler.Cli.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Bayan.Compiler.Cli", "bin", "Release", "net9.0", "Bayan.Compiler.Cli.dll"),
            Path.Combine(Environment.CurrentDirectory, "publish", "Bayan.Compiler.Cli", "Bayan.Compiler.Cli.exe"),
            Path.Combine(Environment.CurrentDirectory, "src", "Bayan.Compiler.Cli", "bin", "Debug", "net9.0", "Bayan.Compiler.Cli.dll")
        };

        foreach (string candidate in candidates)
        {
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch { }
        }

        throw new FileNotFoundException(
            "لم يتم العثور على ملف المترجم المستقل (Bayan.Compiler.Cli.exe أو Bayan.Compiler.Cli.dll) في مسار التشغيل الحالي.");
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
