namespace Estudio.Setup.Core;

public sealed record DesiredStateSetupReport(
    bool Success,
    IReadOnlyList<DesiredStateNodeReport> Nodes)
{
    public SetupReport ToSetupReport()
    {
        var executions = new List<StepExecution>();
        var lastSuccessfulNode = "start";

        foreach (var node in Nodes)
        {
            executions.Add(new StepExecution(node.NodeId, "detect", node.DetectResult.ToStepResult()));
            executions.Add(new StepExecution(node.NodeId, "plan", node.Plan.ToResult().ToStepResult()));
            if (node.ActionResult is not null && !string.IsNullOrWhiteSpace(node.ActionPhase))
            {
                executions.Add(new StepExecution(node.NodeId, node.ActionPhase!, node.ActionResult.ToStepResult()));
            }

            executions.Add(new StepExecution(node.NodeId, "verify", node.VerifyResult.ToStepResult()));
            if (node.VerifyResult.IsReady)
            {
                lastSuccessfulNode = node.NodeId;
            }
        }

        return Success ? SetupReport.Passed(executions) : SetupReport.Failed(lastSuccessfulNode, executions);
    }
}

public sealed record DesiredStateNodeReport(
    string NodeId,
    string NodeName,
    SetupNodeResult DetectResult,
    SetupNodePlan Plan,
    string? ActionPhase,
    SetupNodeResult? ActionResult,
    SetupNodeResult VerifyResult);