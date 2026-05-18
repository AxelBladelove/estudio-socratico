namespace Estudio.Setup.Core;

public sealed record SetupExecutionArtifacts(
    SetupExecutionEngine Engine,
    bool Success,
    string StatePath,
    string LogPath,
    string ReportPath,
    string Alias,
    string LastSuccessfulBlockId,
    IReadOnlyList<SetupExecutionBlock> Blocks)
{
    public static SetupExecutionArtifacts FromLegacy(SetupRunArtifacts artifacts)
    {
        return new SetupExecutionArtifacts(
            SetupExecutionEngine.Legacy,
            artifacts.Report.Success,
            artifacts.StatePath,
            artifacts.LogPath,
            artifacts.ReportPath,
            artifacts.Alias,
            artifacts.Report.LastSuccessfulStep,
            SetupExecutionSummary.FromLegacy(artifacts.Report));
    }

    public static SetupExecutionArtifacts FromDesiredState(DesiredStateSetupRunArtifacts artifacts)
    {
        return new SetupExecutionArtifacts(
            SetupExecutionEngine.DesiredState,
            artifacts.Report.Success,
            artifacts.StatePath,
            artifacts.LogPath,
            artifacts.ReportPath,
            artifacts.Alias,
            SetupExecutionSummary.LastSuccessfulDesiredStateBlockId(artifacts.Report),
            SetupExecutionSummary.FromDesiredState(artifacts.Report));
    }
}

public sealed record SetupExecutionBlock(
    string Id,
    string Name,
    SetupExecutionBlockStatus Status,
    string HumanMessage,
    string TechnicalMessage);

public enum SetupExecutionBlockStatus
{
    Ready,
    Applied,
    Repaired,
    Pending,
    Warning,
    Failed,
}

public static class SetupExecutionSummary
{
    public static IReadOnlyList<SetupExecutionBlock> FromLegacy(SetupReport report)
    {
        return report.Steps
            .Select(step => new SetupExecutionBlock(
                $"{step.StepId}.{step.Phase}",
                $"{step.StepId}.{step.Phase}",
                StatusFor(step.Result),
                step.Result.Message,
                step.Result.Message))
            .ToArray();
    }

    public static IReadOnlyList<SetupExecutionBlock> FromDesiredState(DesiredStateSetupReport report)
    {
        return report.Nodes
            .Select(node => new SetupExecutionBlock(
                node.NodeId,
                node.NodeName,
                StatusFor(node),
                HumanMessageFor(node),
                TechnicalMessageFor(node)))
            .ToArray();
    }

    public static string LastSuccessfulDesiredStateBlockId(DesiredStateSetupReport report)
    {
        return report.Nodes
            .Where(node => node.VerifyResult.IsReady)
            .Select(node => node.NodeId)
            .LastOrDefault() ?? "start";
    }

    private static SetupExecutionBlockStatus StatusFor(StepResult result)
    {
        if (result.IsWarning)
        {
            return SetupExecutionBlockStatus.Warning;
        }

        if (result.Success)
        {
            return SetupExecutionBlockStatus.Ready;
        }

        return result.IsMissing ? SetupExecutionBlockStatus.Pending : SetupExecutionBlockStatus.Failed;
    }

    private static SetupExecutionBlockStatus StatusFor(DesiredStateNodeReport node)
    {
        if (node.VerifyResult.IsReady)
        {
            return node.ActionPhase switch
            {
                "repair" => SetupExecutionBlockStatus.Repaired,
                "apply" => SetupExecutionBlockStatus.Applied,
                _ => SetupExecutionBlockStatus.Ready,
            };
        }

        if (node.ActionPhase is null && node.Plan.Status == SetupNodeStatus.ActionRequired)
        {
            return SetupExecutionBlockStatus.Pending;
        }

        return SetupExecutionBlockStatus.Failed;
    }

    private static string HumanMessageFor(DesiredStateNodeReport node)
    {
        if (node.VerifyResult.IsReady)
        {
            if (node.ActionResult is not null && !string.IsNullOrWhiteSpace(node.ActionResult.HumanMessage))
            {
                return node.ActionResult.HumanMessage;
            }

            return node.VerifyResult.HumanMessage;
        }

        if (!string.IsNullOrWhiteSpace(node.VerifyResult.HumanMessage))
        {
            return node.VerifyResult.HumanMessage;
        }

        return node.Plan.HumanMessage;
    }

    private static string TechnicalMessageFor(DesiredStateNodeReport node)
    {
        var parts = new[]
        {
            node.DetectResult.TechnicalMessage,
            node.Plan.TechnicalMessage,
            node.ActionResult?.TechnicalMessage,
            node.VerifyResult.TechnicalMessage,
        };

        return string.Join("; ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}