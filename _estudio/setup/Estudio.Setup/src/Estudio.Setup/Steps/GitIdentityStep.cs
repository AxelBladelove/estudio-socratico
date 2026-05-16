using Estudio.Setup.Core;
using Estudio.Setup.Profile;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitIdentityStep : ISetupStep
{
    private readonly ICommandRunner _commandRunner;
    private readonly string _alias;

    public GitIdentityStep(ICommandRunner commandRunner, string alias)
    {
        _commandRunner = commandRunner;
        _alias = alias;
    }

    public string Id => "git-identity";
    public string Name => "Git local identity";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureIdentityAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureIdentityAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureIdentityAsync(cancellationToken);
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var (githubUser, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var expectedEmail = BuildNoReplyEmail(githubUser!);
        var configuredGitHubUser = await ReadGitConfigAsync("github.user", cancellationToken);
        if (!configuredGitHubUser.Success)
        {
            return configuredGitHubUser.Result;
        }

        if (!string.Equals(configuredGitHubUser.Value, githubUser, StringComparison.Ordinal))
        {
            return StepResult.Missing($"Git: github.user es '{configuredGitHubUser.Value}', se esperaba '{githubUser}'.");
        }

        var configuredName = await ReadGitConfigAsync("user.name", cancellationToken);
        if (!configuredName.Success)
        {
            return configuredName.Result;
        }

        if (!string.Equals(configuredName.Value, _alias, StringComparison.Ordinal))
        {
            return StepResult.Missing($"Git: user.name es '{configuredName.Value}', se esperaba alias '{_alias}'.");
        }

        var configuredEmail = await ReadGitConfigAsync("user.email", cancellationToken);
        if (!configuredEmail.Success)
        {
            return configuredEmail.Result;
        }

        if (!string.Equals(configuredEmail.Value, expectedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return StepResult.Missing($"Git: user.email es '{configuredEmail.Value}', se esperaba '{expectedEmail}'.");
        }

        return StepResult.Ok("Git: identidad local github.user/user.name/user.email lista.");
    }

    private async Task<StepResult> ConfigureIdentityAsync(CancellationToken cancellationToken)
    {
        LocalStudentProfile.ValidateAlias(_alias);
        var (githubUser, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var expectedEmail = BuildNoReplyEmail(githubUser!);
        var githubConfig = await WriteGitConfigAsync("github.user", githubUser!, cancellationToken);
        if (!githubConfig.Success)
        {
            return githubConfig;
        }

        var nameConfig = await WriteGitConfigAsync("user.name", _alias, cancellationToken);
        if (!nameConfig.Success)
        {
            return nameConfig;
        }

        var emailConfig = await WriteGitConfigAsync("user.email", expectedEmail, cancellationToken);
        if (!emailConfig.Success)
        {
            return emailConfig;
        }

        return StepResult.Ok($"Git: identidad local configurada para {githubUser}/{_alias}.");
    }

    private async Task<(bool Success, string Value, StepResult Result)> ReadGitConfigAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync("git", $"config --local --get {name}", cancellationToken);
        if (!result.WasStarted)
        {
            return (false, string.Empty, StepResult.Missing("Git: git no esta disponible para leer identidad local."));
        }

        if (!result.IsSuccess)
        {
            return (false, string.Empty, StepResult.Missing($"Git: falta config local {name}."));
        }

        return (true, FirstNonEmptyLine(result.StandardOutput), StepResult.Ok($"{name} ok"));
    }

    private async Task<StepResult> WriteGitConfigAsync(
        string name,
        string value,
        CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync("git", $"config --local {name} {value}", cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing("Git: git no esta disponible para escribir identidad local.");
        }

        return result.IsSuccess
            ? StepResult.Ok($"{name} configurado.")
            : StepResult.Fail($"Git: no se pudo configurar {name}. {FirstNonEmptyLine(result.StandardError)}");
    }

    private static string BuildNoReplyEmail(string githubUser)
    {
        return $"{githubUser}@users.noreply.github.com";
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
