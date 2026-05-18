namespace Estudio.Setup.Core;

public sealed class DesiredStateSetupEngine
{
    private readonly IReadOnlyList<ISetupStateNode> _nodes;
    private readonly IDesiredStateSetupProgressSink _progress;

    public DesiredStateSetupEngine(IEnumerable<ISetupStateNode> nodes, IDesiredStateSetupProgressSink? progress = null)
    {
        _nodes = nodes.ToArray();
        _progress = progress ?? NullDesiredStateSetupProgressSink.Instance;
    }

    public async Task<DesiredStateSetupReport> RunAsync(SetupOptions options, CancellationToken cancellationToken)
    {
        if (options.Mode is SetupMode.Package or SetupMode.Uninstall)
        {
            throw new InvalidOperationException($"Desired state setup no soporta el modo {options.Mode} todavia.");
        }

        await _progress.ReportAsync(new DesiredStateRunStarted(options.Mode), cancellationToken);

        var context = new SetupContext(options);
        var reports = new List<DesiredStateNodeReport>();
        foreach (var node in SelectedNodes(options))
        {
            var detect = await RunNodePhaseAsync(
                node,
                "detect",
                $"Revisando {node.Name.ToLowerInvariant()}...",
                (setupContext, token) => node.DetectAsync(setupContext, token),
                context,
                cancellationToken);

            var plan = await RunNodePlanAsync(node, context, detect, cancellationToken);

            string? actionPhase = null;
            SetupNodeResult? actionResult = null;
            if (ShouldMutate(options.Mode, plan))
            {
                if (ShouldRepair(options.Mode, plan))
                {
                    var repairAction = plan.RepairActions.FirstOrDefault()
                        ?? new SetupRepairAction(
                            $"repair-{node.Id}",
                            $"Voy a reparar {node.Name.ToLowerInvariant()}.",
                            $"{node.Id}: no se definio accion de reparacion explicita.");
                    actionPhase = "repair";
                    actionResult = await RunNodePhaseAsync(
                        node,
                        actionPhase,
                        repairAction.HumanMessage,
                        (setupContext, token) => node.RepairAsync(setupContext, repairAction, token),
                        context,
                        cancellationToken);
                }
                else if (plan.ApplyActions.Count > 0)
                {
                    actionPhase = "apply";
                    actionResult = await RunNodePhaseAsync(
                        node,
                        actionPhase,
                        plan.HumanMessage,
                        (setupContext, token) => node.ApplyAsync(setupContext, plan, token),
                        context,
                        cancellationToken);
                }
            }

            var verify = await RunNodePhaseAsync(
                node,
                "verify",
                $"Comprobando {node.Name.ToLowerInvariant()}...",
                (setupContext, token) => node.VerifyAsync(setupContext, token),
                context,
                cancellationToken);

            reports.Add(new DesiredStateNodeReport(node.Id, node.Name, detect, plan, actionPhase, actionResult, verify));
        }

        var report = new DesiredStateSetupReport(reports.All(node => node.VerifyResult.IsReady), reports);
        await _progress.ReportAsync(new DesiredStateRunFinished(report), cancellationToken);
        return report;
    }

    private IEnumerable<ISetupStateNode> SelectedNodes(SetupOptions options)
    {
        if (options.OnlyStepIds is not { Count: > 0 })
        {
            return _nodes;
        }

        var selected = new HashSet<string>(options.OnlyStepIds, StringComparer.OrdinalIgnoreCase);
        return _nodes.Where(node => selected.Contains(node.Id));
    }

    private static bool ShouldMutate(SetupMode mode, SetupNodePlan plan)
    {
        return mode != SetupMode.Verify && plan.RequiresChanges;
    }

    private static bool ShouldRepair(SetupMode mode, SetupNodePlan plan)
    {
        if (plan.RepairActions.Count == 0)
        {
            return false;
        }

        return mode is SetupMode.Repair or SetupMode.Reinstall
            || plan.Status is SetupNodeStatus.RepairRequired or SetupNodeStatus.Failed;
    }

    private async Task<SetupNodePlan> RunNodePlanAsync(
        ISetupStateNode node,
        SetupContext context,
        SetupNodeResult detectResult,
        CancellationToken cancellationToken)
    {
        await _progress.ReportAsync(
            new DesiredStateNodePhaseStarted(node.Id, node.Name, "plan", $"Planeando {node.Name.ToLowerInvariant()}..."),
            cancellationToken);

        SetupNodePlan plan;
        try
        {
            plan = await node.PlanAsync(context, detectResult, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            plan = new SetupNodePlan(
                node.Id,
                node.Name,
                SetupNodeStatus.Failed,
                $"No pude decidir como preparar {node.Name.ToLowerInvariant()}.",
                $"{node.Id}.plan: {ex.Message}",
                RequiresChanges: false,
                ApplyActions: Array.Empty<SetupPlannedAction>(),
                RepairActions: Array.Empty<SetupRepairAction>());
        }

        await _progress.ReportAsync(
            new DesiredStateNodePhaseFinished(node.Id, node.Name, "plan", plan.ToResult()),
            cancellationToken);

        return plan;
    }

    private async Task<SetupNodeResult> RunNodePhaseAsync(
        ISetupStateNode node,
        string phase,
        string humanMessage,
        Func<SetupContext, CancellationToken, Task<SetupNodeResult>> action,
        SetupContext context,
        CancellationToken cancellationToken)
    {
        await _progress.ReportAsync(new DesiredStateNodePhaseStarted(node.Id, node.Name, phase, humanMessage), cancellationToken);

        SetupNodeResult result;
        try
        {
            result = await action(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = new SetupNodeResult(
                node.Id,
                node.Name,
                SetupNodeStatus.Failed,
                $"No pude completar {node.Name.ToLowerInvariant()}.",
                $"{node.Id}.{phase}: {ex.Message}",
                Array.Empty<StepExecution>());
        }

        await _progress.ReportAsync(new DesiredStateNodePhaseFinished(node.Id, node.Name, phase, result), cancellationToken);
        return result;
    }
}