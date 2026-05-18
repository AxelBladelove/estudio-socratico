namespace Estudio.Setup.Core;

public sealed record SetupNodeResult(
    string NodeId,
    string NodeName,
    SetupNodeStatus Status,
    string HumanMessage,
    string TechnicalMessage,
    IReadOnlyList<StepExecution> StepExecutions)
{
    public bool IsReady => Status == SetupNodeStatus.Ready;

    public StepResult ToStepResult()
    {
        var message = string.IsNullOrWhiteSpace(TechnicalMessage)
            ? HumanMessage
            : $"{HumanMessage} | {TechnicalMessage}";

        return Status switch
        {
            SetupNodeStatus.Ready => StepResult.Ok(message),
            SetupNodeStatus.ActionRequired => StepResult.Missing(message),
            SetupNodeStatus.RepairRequired => StepResult.Fail(message),
            SetupNodeStatus.Failed => StepResult.Fail(message),
            _ => StepResult.Fail(message),
        };
    }
}