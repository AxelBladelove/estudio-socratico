using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitSafetyBackupStep : ISetupStep, INonBlockingSetupStep
{
    public const string BackupCommitMessage = "chore(estudio): backup automatico antes de actualizar";

    private readonly ICommandRunner _commandRunner;
    private readonly CommandExecutionOptions _workspaceExecution;

    public GitSafetyBackupStep(ICommandRunner commandRunner, string? workspaceRoot = null)
    {
        _commandRunner = commandRunner;
        _workspaceExecution = new CommandExecutionOptions(WorkingDirectory: workspaceRoot ?? Directory.GetCurrentDirectory());
    }

    public string Id => "git-safety-backup";
    public string Name => "Git safety backup";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckStatusAsync(cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CreateBackupCommitIfNeededAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CreateBackupCommitIfNeededAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CreateBackupCommitIfNeededAsync(cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckStatusAsync(cancellationToken);
    }

    private async Task<StepResult> CheckStatusAsync(CancellationToken cancellationToken)
    {
        var status = await _commandRunner.RunAsync("git", "status --porcelain", _workspaceExecution, cancellationToken);
        if (!status.WasStarted)
        {
            return StepResult.Missing("Git: git no esta disponible para crear backup.");
        }

        if (!status.IsSuccess)
        {
            return StepResult.Fail($"Git: no se pudo leer estado del repo. {FirstNonEmptyLine(status.StandardError)}");
        }

        return string.IsNullOrWhiteSpace(status.StandardOutput)
            ? StepResult.Ok("Git: arbol de trabajo limpio.")
            : StepResult.Warning("Git: hay cambios locales; se creara backup automatico antes de operaciones mutantes.");
    }

    private async Task<StepResult> CreateBackupCommitIfNeededAsync(CancellationToken cancellationToken)
    {
        var status = await _commandRunner.RunAsync("git", "status --porcelain", _workspaceExecution, cancellationToken);
        if (!status.WasStarted)
        {
            return StepResult.Missing("Git: git no esta disponible para crear backup.");
        }

        if (!status.IsSuccess)
        {
            return StepResult.Fail($"Git: no se pudo leer estado del repo. {FirstNonEmptyLine(status.StandardError)}");
        }

        if (string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            return StepResult.Ok("Git: no hay cambios locales que respaldar.");
        }

        var add = await _commandRunner.RunAsync("git", "add -A", _workspaceExecution, cancellationToken);
        if (!add.IsSuccess)
        {
            return StepResult.Fail($"Git: no se pudo preparar backup. {FirstNonEmptyLine(add.StandardError)}");
        }

        var commit = await _commandRunner.RunAsync(
            "git",
            $"commit -m \"{BackupCommitMessage}\"",
            _workspaceExecution,
            cancellationToken);
        if (commit.IsSuccess)
        {
            return StepResult.Ok("Git: backup automatico creado.");
        }

        var details = $"{commit.StandardOutput}\n{commit.StandardError}";
        if (details.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
        {
            return StepResult.Ok("Git: no habia cambios que respaldar despues de preparar archivos.");
        }

        return StepResult.Fail($"Git: no se pudo crear backup automatico. {FirstNonEmptyLine(commit.StandardError)}");
    }

    private static string FirstNonEmptyLine(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim();
            }
        }

        return string.Empty;
    }
}
