using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitRemoteStep : ISetupStep
{
    public const string MainRepoUrl = "https://github.com/AxelBladelove/estudio-socratico.git";

    private readonly ICommandRunner _commandRunner;
    private readonly string _alias;
    private readonly CommandExecutionOptions _workspaceExecution;

    public GitRemoteStep(ICommandRunner commandRunner, string alias, string? workspaceRoot = null)
    {
        _commandRunner = commandRunner;
        _alias = alias;
        _workspaceExecution = new CommandExecutionOptions(WorkingDirectory: workspaceRoot ?? Directory.GetCurrentDirectory());
    }

    public string Id => "git-remotes";
    public string Name => "Git remotes";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureRemotesAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureRemotesAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureRemotesAsync(cancellationToken);
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var (expectedOrigin, error) = await ResolveExpectedOriginAsync(cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var expectedOriginUrl = expectedOrigin ?? throw new InvalidOperationException("No se pudo resolver origin esperado.");
        var origin = await GetRemoteUrlAsync("origin", cancellationToken);
        if (!origin.Success)
        {
            return origin.Result;
        }

        if (!RemoteMatches(origin.Url, expectedOriginUrl))
        {
            return StepResult.Missing($"Git: origin no apunta al fork esperado {expectedOriginUrl}.");
        }

        var upstream = await GetRemoteUrlAsync("upstream", cancellationToken);
        if (!upstream.Success)
        {
            return upstream.Result;
        }

        if (!RemoteMatches(upstream.Url, MainRepoUrl))
        {
            return StepResult.Missing($"Git: upstream no apunta al repo principal {MainRepoUrl}.");
        }

        return StepResult.Ok("Git: remotes origin/upstream listos.");
    }

    private async Task<StepResult> ConfigureRemotesAsync(CancellationToken cancellationToken)
    {
        var (expectedOrigin, error) = await ResolveExpectedOriginAsync(cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var expectedOriginUrl = expectedOrigin ?? throw new InvalidOperationException("No se pudo resolver origin esperado.");
        var origin = await GetRemoteUrlAsync("origin", cancellationToken);
        var originCommand = origin.Success
            ? $"remote set-url origin {expectedOriginUrl}"
            : $"remote add origin {expectedOriginUrl}";
        var originUpdate = await _commandRunner.RunAsync("git", originCommand, _workspaceExecution, cancellationToken);
        if (!originUpdate.IsSuccess)
        {
            return StepResult.Fail($"Git: no se pudo configurar origin. {FirstNonEmptyLine(originUpdate.StandardError)}");
        }

        var upstream = await GetRemoteUrlAsync("upstream", cancellationToken);
        var upstreamCommand = upstream.Success
            ? $"remote set-url upstream {MainRepoUrl}"
            : $"remote add upstream {MainRepoUrl}";
        var upstreamUpdate = await _commandRunner.RunAsync("git", upstreamCommand, _workspaceExecution, cancellationToken);
        if (!upstreamUpdate.IsSuccess)
        {
            return StepResult.Fail($"Git: no se pudo configurar upstream. {FirstNonEmptyLine(upstreamUpdate.StandardError)}");
        }

        return StepResult.Ok("Git: remotes origin/upstream configurados.");
    }

    private async Task<(string? ExpectedOrigin, StepResult? Error)> ResolveExpectedOriginAsync(CancellationToken cancellationToken)
    {
        var (user, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        return ($"https://github.com/{user}/{GitHubForkStep.BuildForkName(_alias)}.git", null);
    }

    private async Task<(bool Success, string Url, StepResult Result)> GetRemoteUrlAsync(
        string remoteName,
        CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync("git", $"remote get-url {remoteName}", _workspaceExecution, cancellationToken);
        if (!result.WasStarted)
        {
            return (false, string.Empty, StepResult.Missing("Git: git no esta disponible para leer remotes."));
        }

        if (!result.IsSuccess)
        {
            return (false, string.Empty, StepResult.Missing($"Git: falta remote {remoteName}."));
        }

        return (true, FirstNonEmptyLine(result.StandardOutput), StepResult.Ok($"{remoteName} ok"));
    }

    private static bool RemoteMatches(string actual, string expected)
    {
        return string.Equals(NormalizeRemote(actual), NormalizeRemote(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRemote(string remote)
    {
        var normalized = remote.Trim().TrimEnd('/');

        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "github.com/" + normalized["git@github.com:".Length..];
        }
        else
        {
            normalized = normalized
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("ssh://git@", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized.TrimEnd('/');
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
