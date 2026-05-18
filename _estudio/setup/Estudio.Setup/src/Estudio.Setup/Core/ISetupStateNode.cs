namespace Estudio.Setup.Core;

public interface ISetupStateNode
{
    string Id { get; }
    string Name { get; }

    Task<SetupNodeResult> DetectAsync(SetupContext context, CancellationToken cancellationToken);
    Task<SetupNodePlan> PlanAsync(SetupContext context, SetupNodeResult detectedState, CancellationToken cancellationToken);
    Task<SetupNodeResult> ApplyAsync(SetupContext context, SetupNodePlan plan, CancellationToken cancellationToken);
    Task<SetupNodeResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken);
    Task<SetupNodeResult> RepairAsync(SetupContext context, SetupRepairAction repairAction, CancellationToken cancellationToken);
}