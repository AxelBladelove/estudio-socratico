using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitWorkspaceStep : ISetupStep
{
    private readonly ICommandRunner _commandRunner;
    private readonly string _alias;
    private readonly string _workspaceRoot;

    public GitWorkspaceStep(ICommandRunner commandRunner, string alias, string workspaceRoot)
    {
        _commandRunner = commandRunner;
        _alias = alias;
        _workspaceRoot = workspaceRoot;
    }

    public string Id => "git-workspace";
    public string Name => "Git workspace";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureWorkspaceAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureWorkspaceAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureWorkspaceAsync(cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(_workspaceRoot, ".git")))
        {
            return Task.FromResult(StepResult.Ok($"Workspace Git listo en {_workspaceRoot}."));
        }

        return Task.FromResult(StepResult.Missing($"Workspace Git faltante en {_workspaceRoot}."));
    }

    private async Task<StepResult> EnsureWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(_workspaceRoot, ".git")))
        {
            return StepResult.Ok($"Workspace Git reutilizado en {_workspaceRoot}.");
        }

        if (Directory.Exists(_workspaceRoot)
            && Directory.EnumerateFileSystemEntries(_workspaceRoot).Any())
        {
            return StepResult.Fail($"Workspace: la carpeta {_workspaceRoot} ya existe y no esta vacia; no se puede clonar encima.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_workspaceRoot) ?? _workspaceRoot);
        var (githubUser, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var originUrl = $"https://github.com/{githubUser}/{GitHubForkStep.BuildForkName(_alias)}.git";
        var clone = await _commandRunner.RunAsync(
            "git",
            $"clone {Quote(originUrl)} {Quote(_workspaceRoot)}",
            cancellationToken);
        if (!clone.WasStarted)
        {
            return StepResult.Missing("Git: git no esta disponible para clonar el workspace del estudiante.");
        }

        if (!clone.IsSuccess)
        {
            return StepResult.Fail($"Git: no se pudo clonar el workspace en {_workspaceRoot}. {FirstNonEmptyLine(clone.StandardError, clone.StandardOutput)}");
        }

        return StepResult.Ok($"Workspace Git creado en {_workspaceRoot}.");
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string FirstNonEmptyLine(params string[] texts)
    {
        foreach (var text in texts)
        {
            using var reader = new StringReader(text);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }
        }

        return string.Empty;
    }
}