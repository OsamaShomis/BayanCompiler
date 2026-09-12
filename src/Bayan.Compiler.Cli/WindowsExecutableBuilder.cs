using System.Diagnostics;

namespace Bayan.Compiler.Cli;

/// <summary>
/// Invokes the Windows IL assembler for a CIL artifact without moving target-specific tooling into Core.
/// </summary>
internal static class WindowsExecutableBuilder
{
    /// <summary>
    /// Builds one managed Windows executable from a persisted IL file when ILAsm is available on Windows.
    /// </summary>
    public static WindowsExecutableBuildResult TryBuild(string ilPath, string executablePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WindowsExecutableBuildResult(false, false, string.Empty, "[EXE001] ilasm.exe is invoked only on Windows.");
        }

        string ilasmPath = Environment.GetEnvironmentVariable("BAYAN_ILASM_PATH") ?? "ilasm.exe";
        var startInfo = new ProcessStartInfo
        {
            FileName = ilasmPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(ilPath) ?? Environment.CurrentDirectory
        };
        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/quiet");
        startInfo.ArgumentList.Add("/exe");
        startInfo.ArgumentList.Add("/output:" + executablePath);
        startInfo.ArgumentList.Add(ilPath);

        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start ilasm.exe.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(true);
                return new WindowsExecutableBuildResult(true, false, string.Empty, "[EXE003] انتهت مهلة ilasm.exe بعد 30 ثانية.");
            }

            if (process.ExitCode != 0 || !File.Exists(executablePath))
            {
                string details = string.Join(Environment.NewLine, new[] { standardOutput, standardError }.Where(text => !string.IsNullOrWhiteSpace(text)));
                return new WindowsExecutableBuildResult(true, false, string.Empty, "[EXE002] فشل ilasm.exe." + Environment.NewLine + details);
            }

            return new WindowsExecutableBuildResult(true, true, executablePath, string.Empty);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            return new WindowsExecutableBuildResult(true, false, string.Empty, "[EXE001] تعذر العثور على ilasm.exe. اضبط BAYAN_ILASM_PATH أو شغّل من Visual Studio Developer Command Prompt." + Environment.NewLine + exception.Message);
        }
    }
}

/// <summary>
/// Represents the outcome of one attempt to assemble CIL into a Windows executable.
/// </summary>
internal sealed record WindowsExecutableBuildResult(bool Attempted, bool Succeeded, string ExecutablePath, string Diagnostic);
