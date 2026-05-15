namespace Estudio.Setup.Core;

public interface ISetupStep
{
    string Id { get; }
    string Name { get; }

    Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken);
    Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken);
    Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken);
    Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken);
    Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken);
}
