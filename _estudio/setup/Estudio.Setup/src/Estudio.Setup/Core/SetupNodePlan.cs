namespace Estudio.Setup.Core;

public sealed record SetupNodePlan(
    string NodeId,
    string NodeName,
    SetupNodeStatus Status,
    string HumanMessage,
    string TechnicalMessage,
    bool RequiresChanges,
    IReadOnlyList<SetupPlannedAction> ApplyActions,
    IReadOnlyList<SetupRepairAction> RepairActions)
{
    public SetupNodeResult ToResult()
    {
        return new SetupNodeResult(
            NodeId,
            NodeName,
            Status,
            HumanMessage,
            TechnicalMessage,
            Array.Empty<StepExecution>());
    }
}

public sealed record SetupPlannedAction(string StepId, string Phase);