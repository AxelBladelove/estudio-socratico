using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitProjectUpdateStep : ISetupStep
{
    private readonly ICommandRunner _commandRunner;
    private readonly CommandExecutionOptions _workspaceExecution;

    public GitProjectUpdateStep(ICommandRunner commandRunner, string? workspaceRoot = null)
    {
        _commandRunner = commandRunner;
        _workspaceExecution = new CommandExecutionOptions(WorkingDirectory: workspaceRoot ?? Directory.GetCurrentDirectory());
    }

    public string Id => "git-project-update";
    public string Name => "Git project update";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return UpdateProjectAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var upstream = await _commandRunner.RunAsync("git", "ls-remote upstream main", _workspaceExecution, cancellationToken);
        if (!upstream.WasStarted)
        {
            return StepResult.Missing("Git: git no esta disponible para verificar upstream.");
        }

        if (!upstream.IsSuccess)
        {
            return StepResult.Missing($"Git: upstream/main no esta disponible. {FirstNonEmptyLine(upstream.StandardError)}");
        }

        var origin = await _commandRunner.RunAsync("git", "ls-remote origin main", _workspaceExecution, cancellationToken);
        if (!origin.IsSuccess)
        {
            return StepResult.Missing($"Git: origin/main no esta disponible. {FirstNonEmptyLine(origin.StandardError)}");
        }

        return StepResult.Ok("Git: upstream/main y origin/main alcanzables.");
    }

    private async Task<StepResult> UpdateProjectAsync(CancellationToken cancellationToken)
    {
        var fetch = await _commandRunner.RunAsync("git", "fetch upstream", _workspaceExecution, cancellationToken);
        if (!fetch.IsSuccess)
        {
            return StepResult.Fail($"Git: fetch upstream fallo. {FirstNonEmptyLine(fetch.StandardError, fetch.StandardOutput)}");
        }

        var merge = await _commandRunner.RunAsync("git", "merge upstream/main", _workspaceExecution, cancellationToken);
        if (!merge.IsSuccess)
        {
            return StepResult.Fail($"Git: merge upstream/main fallo. {FirstNonEmptyLine(merge.StandardError, merge.StandardOutput)}");
        }

        var push = await _commandRunner.RunAsync("git", "push origin main", _workspaceExecution, cancellationToken);
        if (!push.IsSuccess)
        {
            var detail = FirstNonEmptyLine(push.StandardError, push.StandardOutput);
            if (IsReadOnlyOrigin(detail))
            {
                return StepResult.Warning($"Git: proyecto actualizado desde upstream/main, pero origin/main quedo sin publicar porque el remoto es de solo lectura. {detail}");
            }

            return StepResult.Fail($"Git: push origin main fallo. {detail}");
        }

        return StepResult.Ok("Git: proyecto actualizado desde upstream/main y publicado en origin/main.");
    }

    private static bool IsReadOnlyOrigin(string detail)
    {
        return detail.Contains("read-only", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("read only", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("archived", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("solo lectura", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("solo-lectura", StringComparison.OrdinalIgnoreCase);
    }

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
