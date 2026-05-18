using System.ComponentModel;
using System.Diagnostics;

namespace Estudio.Setup.Services;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        CommandExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        var effectivePath = ProcessEnvironmentPath.Merge(
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));
        var resolvedFileName = CommandPathResolver.Resolve(
            fileName,
            effectivePath,
            Environment.GetEnvironmentVariable("PATHEXT"));
        var startInfo = new ProcessStartInfo
        {
            FileName = CommandPathResolver.IsWindowsCommandScript(resolvedFileName) ? "cmd.exe" : resolvedFileName,
            Arguments = CommandPathResolver.IsWindowsCommandScript(resolvedFileName)
                ? BuildCommandScriptArguments(resolvedFileName, arguments)
                : arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(executionOptions.WorkingDirectory))
        {
            startInfo.WorkingDirectory = executionOptions.WorkingDirectory;
        }

        startInfo.Environment["PATH"] = effectivePath;

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return CommandResult.NotFound(fileName);
            }
        }
        catch (Win32Exception)
        {
            return CommandResult.NotFound(fileName);
        }
        catch (FileNotFoundException)
        {
            return CommandResult.NotFound(fileName);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new CommandResult(process.ExitCode, stdout, stderr, true);
    }

    private static string BuildCommandScriptArguments(string fileName, string arguments)
    {
        return string.IsNullOrWhiteSpace(arguments)
            ? $"/d /s /c \"\"{fileName}\"\""
            : $"/d /s /c \"\"{fileName}\" {arguments}\"";
    }
}
