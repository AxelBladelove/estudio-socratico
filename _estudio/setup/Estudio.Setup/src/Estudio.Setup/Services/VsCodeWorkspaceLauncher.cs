using System.Diagnostics;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Services;

public sealed class VsCodeWorkspaceLauncher : IVsCodeWorkspaceLauncher
{
    private readonly IUserEnvironment _userEnvironment;

    public VsCodeWorkspaceLauncher(IUserEnvironment? userEnvironment = null)
    {
        _userEnvironment = userEnvironment ?? new UserEnvironment();
    }

    public void OpenWorkspace(string workspaceRoot, string studentAlias, string? exercismToken = null)
    {
        var startInfo = BuildStartInfo(workspaceRoot, studentAlias, exercismToken);
        Process.Start(startInfo);
    }

    internal ProcessStartInfo BuildStartInfo(string workspaceRoot, string studentAlias, string? exercismToken)
    {
        var codeCommand = VsCodeCliPathResolver.ResolveCodeCommand();
        var shellFileName = codeCommand.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
            : codeCommand;
        var arguments = codeCommand.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            ? $"/c \"\"{codeCommand}\" \"{workspaceRoot}\"\""
            : $"\"{workspaceRoot}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = shellFileName,
            Arguments = arguments,
            WorkingDirectory = workspaceRoot,
            UseShellExecute = false,
        };

        var mergedPath = MergePath(Environment.GetEnvironmentVariable("PATH"), _userEnvironment.GetUserVariable("PATH"));
        startInfo.Environment["PATH"] = mergedPath;
        startInfo.Environment["ESTUDIO_USUARIO"] = studentAlias;
        if (!string.IsNullOrWhiteSpace(exercismToken))
        {
            startInfo.Environment[Steps.ExercismCTrackStep.TokenEnvironmentVariable] = exercismToken;
        }

        return startInfo;
    }

    private static string MergePath(string? processPath, string? userPath)
    {
        var entries = new List<string>();
        AddEntries(entries, processPath);
        AddEntries(entries, userPath);
        return string.Join(Path.PathSeparator, entries);
    }

    private static void AddEntries(List<string> entries, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!entries.Any(existing => string.Equals(existing.TrimEnd('\\', '/'), entry.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            {
                entries.Add(entry);
            }
        }
    }
}