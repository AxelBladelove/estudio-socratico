using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitHubForkStep : ISetupStep
{
    public const string MainRepo = "AxelBladelove/estudio-socratico";

    private readonly ICommandRunner _commandRunner;
    private readonly string _alias;

    public GitHubForkStep(ICommandRunner commandRunner, string alias)
    {
        _commandRunner = commandRunner;
        _alias = alias;
    }

    public string Id => "github-fork";
    public string Name => "GitHub fork";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureForkAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureForkAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureForkAsync(cancellationToken);
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var (user, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var forkName = BuildForkName(_alias);
        var repo = $"{user}/{forkName}";
        var view = await _commandRunner.RunAsync("gh", $"repo view {repo} --json name --jq .name", cancellationToken);
        if (!view.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para verificar el fork.");
        }

        if (!view.IsSuccess)
        {
            return StepResult.Missing($"GitHub: falta fork esperado {repo}.");
        }

        return StepResult.Ok($"GitHub: fork {repo} disponible.");
    }

    private async Task<StepResult> EnsureForkAsync(CancellationToken cancellationToken)
    {
        var (user, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var forkName = BuildForkName(_alias);
        var repo = $"{user}/{forkName}";
        var view = await _commandRunner.RunAsync("gh", $"repo view {repo} --json name --jq .name", cancellationToken);
        if (view.IsSuccess)
        {
            return StepResult.Ok($"GitHub: fork {repo} ya existe.");
        }

        var fork = await _commandRunner.RunAsync(
            "gh",
            $"repo fork {MainRepo} --fork-name {forkName}",
            cancellationToken);
        if (!fork.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para crear el fork.");
        }

        if (!fork.IsSuccess)
        {
            var forkFailure = $"{fork.StandardOutput}\n{fork.StandardError}";
            if (forkFailure.Contains("cannot own both a parent and fork", StringComparison.OrdinalIgnoreCase)
                || forkFailure.Contains("cannot be forked", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateWorkRepositoryAsync(repo, cancellationToken);
            }

            return StepResult.Fail($"GitHub: no se pudo crear fork {repo}. {FirstNonEmptyLine(fork.StandardError)}");
        }

        return StepResult.Ok($"GitHub: fork {repo} creado o actualizado.");
    }

    private async Task<StepResult> CreateWorkRepositoryAsync(string repo, CancellationToken cancellationToken)
    {
        var create = await _commandRunner.RunAsync(
            "gh",
            $"repo create {repo} --public --description \"Estudio Socratico workspace\"",
            cancellationToken);
        if (!create.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para crear el repo de trabajo.");
        }

        if (!create.IsSuccess)
        {
            return StepResult.Fail($"GitHub: no se pudo crear repo de trabajo {repo}. {FirstNonEmptyLine(create.StandardError)}");
        }

        return StepResult.Ok($"GitHub: repo de trabajo {repo} creado.");
    }

    public static string BuildForkName(string alias) => $"estudio-socratico-{alias}";

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
