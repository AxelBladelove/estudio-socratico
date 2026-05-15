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
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(LoginRequired());
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(LoginRequired());
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(LoginRequired());
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

    private static StepResult LoginRequired()
    {
        return StepResult.Missing("GitHub: falta iniciar sesion. Ejecuta `gh auth login` y vuelve a correr el instalador.");
    }
}
