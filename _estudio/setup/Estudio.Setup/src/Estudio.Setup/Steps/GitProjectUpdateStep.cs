using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitProjectUpdateStep : ISetupStep
{
    private readonly ICommandRunner _commandRunner;

    public GitProjectUpdateStep(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
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
        var upstream = await _commandRunner.RunAsync("git", "ls-remote upstream main", cancellationToken);
        if (!upstream.WasStarted)
        {
            return StepResult.Missing("Git: git no esta disponible para verificar upstream.");
        }

        if (!upstream.IsSuccess)
        {
            return StepResult.Missing($"Git: upstream/main no esta disponible. {FirstNonEmptyLine(upstream.StandardError)}");
        }

        var origin = await _commandRunner.RunAsync("git", "ls-remote origin main", cancellationToken);
        if (!origin.IsSuccess)
        {
            return StepResult.Missing($"Git: origin/main no esta disponible. {FirstNonEmptyLine(origin.StandardError)}");
        }

        return StepResult.Ok("Git: upstream/main y origin/main alcanzables.");
    }

    private async Task<StepResult> UpdateProjectAsync(CancellationToken cancellationToken)
    {
        var fetch = await _commandRunner.RunAsync("git", "fetch upstream", cancellationToken);
        if (!fetch.IsSuccess)
        {
            return StepResult.Fail($"Git: fetch upstream fallo. {FirstNonEmptyLine(fetch.StandardError)}");
        }

        var merge = await _commandRunner.RunAsync("git", "merge upstream/main", cancellationToken);
        if (!merge.IsSuccess)
        {
            return StepResult.Fail($"Git: merge upstream/main fallo. {FirstNonEmptyLine(merge.StandardError)}");
        }

        var push = await _commandRunner.RunAsync("git", "push origin main", cancellationToken);
        if (!push.IsSuccess)
        {
            return StepResult.Fail($"Git: push origin main fallo. {FirstNonEmptyLine(push.StandardError)}");
        }

        return StepResult.Ok("Git: proyecto actualizado desde upstream/main y publicado en origin/main.");
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
