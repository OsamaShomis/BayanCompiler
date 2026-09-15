using System.Diagnostics;
using System.Text;

namespace BayanCompiler.Forms;

/// <summary>
/// Executes a managed Windows executable created by ILAsm and captures its genuine console streams.
/// </summary>
internal sealed class WindowsExecutableRuntimeClient
{
    private Process? activeProcess;

    /// <summary>
    /// Starts one generated executable with redirected output and allows interactive standard input.
    /// </summary>
    public async Task<WindowsExecutableRun> ExecuteInteractiveAsync(
        string executablePath,
        Action<string> onOutput,
        Action<string> onError,
        CancellationToken cancellationToken = default)
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

        activeProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("تعذر بدء program.exe.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var stdoutTask = ReadStreamAsync(activeProcess.StandardOutput, onOutput, timeout.Token);
        var stderrTask = ReadStreamAsync(activeProcess.StandardError, onError, timeout.Token);

        int exitCode = -1;
        try
        {
            await activeProcess.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdoutTask, stderrTask);
            exitCode = activeProcess.ExitCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!activeProcess.HasExited)
            {
                activeProcess.Kill(true);
            }

            throw new TimeoutException("تجاوز program.exe مهلة 5 دقائق.");
        }
        finally
        {
            if (activeProcess != null)
            {
                activeProcess.Dispose();
                activeProcess = null;
            }
        }

        return new WindowsExecutableRun(exitCode, "", "");
    }

    private async Task ReadStreamAsync(StreamReader reader, Action<string> onData, CancellationToken token)
    {
        char[] buffer = new char[256];
        try
        {
            while (!token.IsCancellationRequested)
            {
                int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break;
                onData(new string(buffer, 0, read));
            }
        }
        catch (Exception)
        {
            // Ignore stream read errors on close
        }
    }

    public async Task WriteInputAsync(string input)
    {
        if (activeProcess != null && !activeProcess.HasExited)
        {
            await activeProcess.StandardInput.WriteLineAsync(input);
            await activeProcess.StandardInput.FlushAsync();
        }
    }
}

/// <summary>
/// Represents captured output from one generated Windows executable invocation.
/// </summary>
internal sealed record WindowsExecutableRun(int ExitCode, string StandardOutput, string StandardError);
