using System.Diagnostics;
using System.Text;

namespace BayanCompiler.Forms;

/// <summary>
/// Executes a managed Windows executable created by ILAsm and captures its genuine console streams.
/// </summary>
internal sealed class WindowsExecutableRuntimeClient
{
    /// <summary>
    /// Starts one generated executable with redirected output and a finite timeout.
    /// </summary>
    public async Task<WindowsExecutableRun> ExecuteAsync(string executablePath, string standardInput = "", CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("تشغيل program.exe متاح من محرر Windows فقط.");
        }

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("لم يتم العثور على program.exe الذي ولده ILAsm.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("تعذر بدء program.exe.");
            
        if (!string.IsNullOrEmpty(standardInput))
        {
            await process.StandardInput.WriteAsync(standardInput);
        }
        process.StandardInput.Close();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }

            throw new TimeoutException("تجاوز program.exe مهلة 20 ثانية.");
        }

        return new WindowsExecutableRun(process.ExitCode, await stdoutTask, await stderrTask);
    }
}

/// <summary>
/// Represents captured output from one generated Windows executable invocation.
/// </summary>
internal sealed record WindowsExecutableRun(int ExitCode, string StandardOutput, string StandardError);
