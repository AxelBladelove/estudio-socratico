namespace Estudio.Setup.Core;

public sealed class SetupEngine
{
    private readonly IReadOnlyList<ISetupStep> _steps;
    private readonly ISetupProgressSink _progress;

    public SetupEngine(IEnumerable<ISetupStep> steps, ISetupProgressSink? progress = null)
    {
        _steps = steps.ToArray();
        _progress = progress ?? NullSetupProgressSink.Instance;
    }

    public async Task<SetupReport> RunAsync(SetupOptions options, CancellationToken cancellationToken)
    {
        await _progress.ReportAsync(new SetupRunStarted(options.Mode), cancellationToken);
        var report = options.Mode == SetupMode.Verify
            ? await RunVerifyAsync(options, cancellationToken)
            : options.Mode == SetupMode.Uninstall
                ? await RunUninstallAsync(options, cancellationToken)
            : await RunMutationAsync(options, cancellationToken);
        await _progress.ReportAsync(new SetupRunFinished(report), cancellationToken);
        return report;
    }

    private async Task<SetupReport> RunMutationAsync(SetupOptions options, CancellationToken cancellationToken)
    {
        var context = new SetupContext(options);
        var executions = new List<StepExecution>();
        var lastSuccessfulStep = "start";

        foreach (var step in SelectedSteps(options))
        {
            var detect = await RunPhaseAsync(step, "detect", step.DetectAsync, context, cancellationToken);
            detect = ConvertNonBlockingFailure(step, detect);
            executions.Add(detect);
            if (options.Mode != SetupMode.Repair && !detect.Result.Success && !detect.Result.IsMissing)
            {
                return SetupReport.Failed(lastSuccessfulStep, executions);
            }

            if (ShouldRunModeAction(options.Mode, detect.Result))
            {
                var action = await RunModeActionAsync(step, options.Mode, context, cancellationToken);
                action = ConvertNonBlockingFailure(step, action);
                executions.Add(action);
                if (!action.Result.Success)
                {
                    return SetupReport.Failed(lastSuccessfulStep, executions);
                }
            }

            var verify = await RunPhaseAsync(step, "verify", step.VerifyAsync, context, cancellationToken);
            verify = ConvertNonBlockingFailure(step, verify);
            executions.Add(verify);
            if (!verify.Result.Success)
            {
                return SetupReport.Failed(lastSuccessfulStep, executions);
            }

            lastSuccessfulStep = step.Id;
        }

        return SetupReport.Passed(executions);
    }

    private static bool ShouldRunModeAction(SetupMode mode, StepResult detect)
    {
        return mode switch
        {
            SetupMode.Install => detect.IsMissing || detect.IsWarning,
            SetupMode.Update => true,
            SetupMode.Reinstall => true,
            SetupMode.Repair => detect.IsMissing || detect.IsWarning || !detect.Success,
            SetupMode.Uninstall => true,
            SetupMode.Verify => false,
            _ => false,
        };
    }

    private async Task<SetupReport> RunUninstallAsync(SetupOptions options, CancellationToken cancellationToken)
    {
        var context = new SetupContext(options);
        var executions = new List<StepExecution>();
        var lastSuccessfulStep = "start";

        foreach (var step in SelectedSteps(options))
        {
            var detect = await RunPhaseAsync(step, "detect", step.DetectAsync, context, cancellationToken);
            detect = ConvertNonBlockingFailure(step, detect);
            executions.Add(detect);

            var action = step is IUninstallSetupStep uninstallable
                ? await RunPhaseAsync(step, "uninstall", uninstallable.UninstallAsync, context, cancellationToken)
                : await RunPhaseAsync(
                    step,
                    "uninstall",
                    (_, _) => Task.FromResult(StepResult.Warning($"{step.Name}: no requiere desinstalacion automatica.")),
                    context,
                    cancellationToken);
            action = ConvertNonBlockingFailure(step, action);
            executions.Add(action);
            if (!action.Result.Success)
            {
                return SetupReport.Failed(lastSuccessfulStep, executions);
            }

            lastSuccessfulStep = step.Id;
        }

        return SetupReport.Passed(executions);
    }

    private async Task<SetupReport> RunVerifyAsync(SetupOptions options, CancellationToken cancellationToken)
    {
        var context = new SetupContext(options);
        var executions = new List<StepExecution>();
        var lastSuccessfulStep = "start";
        var success = true;

        foreach (var step in SelectedSteps(options))
        {
            var detect = await RunPhaseAsync(step, "detect", step.DetectAsync, context, cancellationToken);
            detect = ConvertNonBlockingFailure(step, detect);
            executions.Add(detect);
            if (!detect.Result.Success)
            {
                success = false;
                continue;
            }

            var verify = await RunPhaseAsync(step, "verify", step.VerifyAsync, context, cancellationToken);
            verify = ConvertNonBlockingFailure(step, verify);
            executions.Add(verify);
            if (!verify.Result.Success)
            {
                success = false;
                continue;
            }

            if (success)
            {
                lastSuccessfulStep = step.Id;
            }
        }

        return success ? SetupReport.Passed(executions) : SetupReport.Failed(lastSuccessfulStep, executions);
    }

    private IEnumerable<ISetupStep> SelectedSteps(SetupOptions options)
    {
        if (options.OnlyStepIds is not { Count: > 0 })
        {
            return _steps;
        }

        var selected = new HashSet<string>(options.OnlyStepIds, StringComparer.OrdinalIgnoreCase);
        return _steps.Where(step => selected.Contains(step.Id));
    }

    private static StepExecution ConvertNonBlockingFailure(ISetupStep step, StepExecution execution)
    {
        if (step is INonBlockingSetupStep
            && !execution.Result.Success
            && !execution.Result.IsWarning)
        {
            return execution with { Result = execution.Result.AsWarning() };
        }

        return execution;
    }

    private Task<StepExecution> RunModeActionAsync(
        ISetupStep step,
        SetupMode mode,
        SetupContext context,
        CancellationToken cancellationToken)
    {
        return mode switch
        {
            SetupMode.Install => RunPhaseAsync(step, "install", step.InstallAsync, context, cancellationToken),
            SetupMode.Update => RunPhaseAsync(step, "update", step.UpdateAsync, context, cancellationToken),
            SetupMode.Reinstall => RunPhaseAsync(step, "reinstall", step.RepairAsync, context, cancellationToken),
            SetupMode.Repair => RunPhaseAsync(step, "repair", step.RepairAsync, context, cancellationToken),
            SetupMode.Uninstall => throw new InvalidOperationException("Uninstall mode has its own mutation phase."),
            SetupMode.Verify => throw new InvalidOperationException("Verify mode has no mutation phase."),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Modo no soportado."),
        };
    }

    private async Task<StepExecution> RunPhaseAsync(
        ISetupStep step,
        string phase,
        Func<SetupContext, CancellationToken, Task<StepResult>> action,
        SetupContext context,
        CancellationToken cancellationToken)
    {
        await _progress.ReportAsync(new SetupPhaseStarted(step.Id, step.Name, phase), cancellationToken);
        StepExecution execution;
        try
        {
            var result = await action(context, cancellationToken);
            execution = new StepExecution(step.Id, phase, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            execution = new StepExecution(
                step.Id,
                phase,
                StepResult.Fail($"{step.Name}: error inesperado. {ex.Message}"));
        }

        await _progress.ReportAsync(new SetupPhaseFinished(execution), cancellationToken);
        return execution;
    }
}
