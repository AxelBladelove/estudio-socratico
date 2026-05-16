using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitHubAuthStep : ISetupStep
{
    private readonly ICommandRunner _commandRunner;

    public GitHubAuthStep(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public string Id => "github-auth";
    public string Name => "GitHub authentication";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        if (context.Options.ForceGitHubRelogin)
        {
            return Task.FromResult(StepResult.Missing("GitHub: se solicito cambiar la cuenta autenticada."));
        }

        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return LoginAsync(context, cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return context.Options.ForceGitHubRelogin
            ? LoginAsync(context, cancellationToken)
            : VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return context.Options.ForceGitHubRelogin
            ? LoginAsync(context, cancellationToken)
            : VerifyAsync(context, cancellationToken);
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync("gh", "auth status", cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para verificar autenticacion.");
        }

        return result.IsSuccess
            ? StepResult.Ok("GitHub: gh auth status OK.")
            : LoginRequired();
    }

    private async Task<StepResult> LoginAsync(SetupContext context, CancellationToken cancellationToken)
    {
        if (context.Options.ForceGitHubRelogin)
        {
            var logout = await _commandRunner.RunAsync("gh", "auth logout --hostname github.com --yes", cancellationToken);
            if (!logout.WasStarted)
            {
                return StepResult.Missing("GitHub: gh no esta disponible para cerrar la sesion actual.");
            }

            if (!logout.IsSuccess && !LooksLikeNoExistingSession(logout))
            {
                return StepResult.Fail($"GitHub: no se pudo cerrar sesion. {FirstNonEmptyLine(logout.StandardError)}");
            }
        }

        var login = await _commandRunner.RunAsync(
            "gh",
            "auth login --hostname github.com --web --git-protocol https",
            cancellationToken);
        if (!login.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para iniciar sesion.");
        }

        if (!login.IsSuccess)
        {
            return StepResult.Fail($"GitHub: login web no completo correctamente. {FirstNonEmptyLine(login.StandardError)}");
        }

        return await VerifyAsync(context, cancellationToken);
    }

    private static StepResult LoginRequired()
    {
        return StepResult.Missing("GitHub: falta iniciar sesion. Ejecuta `gh auth login` y vuelve a correr el instalador.");
    }

    private static bool LooksLikeNoExistingSession(CommandResult result)
    {
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        return text.Contains("not logged", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not authenticated", StringComparison.OrdinalIgnoreCase)
            || text.Contains("no authenticated", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not currently logged", StringComparison.OrdinalIgnoreCase);
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
