namespace Estudio.Setup.Core;

public interface IUninstallSetupStep
{
    Task<StepResult> UninstallAsync(SetupContext context, CancellationToken cancellationToken);
}
