using Estudio.Setup.Runtime;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Core;

public abstract class StepBackedSetupStateNode : ISetupStateNode
{
    private readonly IReadOnlyList<ISetupStep> _steps;

    protected StepBackedSetupStateNode(string id, string name, IEnumerable<ISetupStep> steps)
    {
        Id = id;
        Name = name;
        _steps = steps.ToArray();
    }

    public string Id { get; }
    public string Name { get; }

    public async Task<SetupNodeResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var executions = await ExecuteAsync("detect", context, (step, setupContext, token) => step.DetectAsync(setupContext, token), cancellationToken);
        return BuildDetectedResult(executions);
    }

    public Task<SetupNodePlan> PlanAsync(SetupContext context, SetupNodeResult detectedState, CancellationToken cancellationToken)
    {
        var status = detectedState.Status;
        if (context.Options.Mode == SetupMode.Reinstall && _steps.Count > 0)
        {
            var reinstallRepair = CreateRepairAction("reinstall", $"Voy a reinstalar {Name.ToLowerInvariant()}.");
            return Task.FromResult(new SetupNodePlan(
                Id,
                Name,
                SetupNodeStatus.RepairRequired,
                reinstallRepair.HumanMessage,
                reinstallRepair.TechnicalMessage,
                RequiresChanges: true,
                ApplyActions: Array.Empty<SetupPlannedAction>(),
                RepairActions: new[] { reinstallRepair }));
        }

        if (status == SetupNodeStatus.Ready)
        {
            return Task.FromResult(new SetupNodePlan(
                Id,
                Name,
                status,
                ReadyHumanMessage,
                detectedState.TechnicalMessage,
                RequiresChanges: false,
                ApplyActions: Array.Empty<SetupPlannedAction>(),
                RepairActions: Array.Empty<SetupRepairAction>()));
        }

        var applyActions = detectedState.StepExecutions
            .Where(NeedsApply)
            .Select(execution => new SetupPlannedAction(execution.StepId, "install"))
            .ToArray();
        var repairActions = new[]
        {
            CreateRepairAction("repair", RepairHumanMessage),
        };

        var humanMessage = status == SetupNodeStatus.RepairRequired
            ? RepairHumanMessage
            : PendingHumanMessage;

        return Task.FromResult(new SetupNodePlan(
            Id,
            Name,
            status,
            humanMessage,
            detectedState.TechnicalMessage,
            RequiresChanges: applyActions.Length > 0 || repairActions.Length > 0,
            ApplyActions: applyActions,
            RepairActions: repairActions));
    }

    public async Task<SetupNodeResult> ApplyAsync(SetupContext context, SetupNodePlan plan, CancellationToken cancellationToken)
    {
        var executions = new List<StepExecution>();
        foreach (var action in plan.ApplyActions)
        {
            var step = FindStep(action.StepId);
            var result = await RunMutationAsync(step, action.Phase, context, cancellationToken);
            executions.Add(new StepExecution(step.Id, action.Phase, result));
        }

        return BuildMutationResult(executions, AppliedHumanMessage);
    }

    public async Task<SetupNodeResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var executions = await ExecuteAsync("verify", context, (step, setupContext, token) => step.VerifyAsync(setupContext, token), cancellationToken);
        return BuildVerifiedResult(executions);
    }

    public async Task<SetupNodeResult> RepairAsync(SetupContext context, SetupRepairAction repairAction, CancellationToken cancellationToken)
    {
        var detected = await DetectAsync(context, cancellationToken);
        var targetSteps = detected.StepExecutions
            .Where(execution => execution.Result.IsWarning || !execution.Result.Success)
            .Select(execution => FindStep(execution.StepId))
            .DistinctBy(step => step.Id)
            .ToArray();

        if (targetSteps.Length == 0)
        {
            return new SetupNodeResult(Id, Name, SetupNodeStatus.Ready, ReadyHumanMessage, repairAction.TechnicalMessage, Array.Empty<StepExecution>());
        }

        var executions = new List<StepExecution>();
        foreach (var step in targetSteps)
        {
            var result = await step.RepairAsync(context, cancellationToken);
            executions.Add(new StepExecution(step.Id, "repair", result));
        }

        return BuildMutationResult(executions, RepairedHumanMessage);
    }

    protected abstract string ReadyHumanMessage { get; }
    protected abstract string PendingHumanMessage { get; }
    protected abstract string RepairHumanMessage { get; }
    protected abstract string AppliedHumanMessage { get; }
    protected abstract string RepairedHumanMessage { get; }

    protected static ISetupStep CreateGitReadyStep(ICommandRunner commandRunner)
    {
        return new WingetPackageStep("git", "Git", "Git.Git", "git", "--version", commandRunner);
    }

    protected static ISetupStep CreateGitHubCliStep(ICommandRunner commandRunner)
    {
        return new WingetPackageStep("github-cli", "GitHub CLI", "GitHub.cli", "gh", "--version", commandRunner);
    }

    protected static ISetupStep CreateVsCodeStep(ICommandRunner commandRunner)
    {
        var codeCommand = VsCodeCliPathResolver.ResolveCodeCommand();
        return new WingetPackageStep("vscode", "Visual Studio Code", "Microsoft.VisualStudioCode", codeCommand, "--version", commandRunner);
    }

    protected static ISetupStep CreateVsCodeSettingsStep(string studentAlias, string? appDataRoot)
    {
        return new VsCodeSettingsStep(
            VsCodeSettingsPaths.ResolveSettingsPath(appDataRoot),
            studentAlias,
            RuntimeConfigPaths.ResolveConfigPath(appDataRoot));
    }

    protected static ISetupStep CreateVsixPackageStep(string workspaceRoot, ICommandRunner commandRunner)
    {
        return new VsixPackageStep(workspaceRoot, commandRunner, AppContext.BaseDirectory);
    }

    protected static ISetupStep CreateVsixExtensionStep(string workspaceRoot, ICommandRunner commandRunner)
    {
        return new VsixExtensionStep(
            VsixExtensionPaths.ResolveInstallableVsixPath(AppContext.BaseDirectory, workspaceRoot),
            VsixExtensionPaths.ExtensionId,
            commandRunner,
            VsCodeCliPathResolver.ResolveCodeCommand());
    }

    protected static ISetupStep CreateCompilerToolchainStep(ICommandRunner commandRunner)
    {
        return new Msys2ToolchainStep(commandRunner);
    }

    protected static ISetupStep CreateCompilerPathStep()
    {
        return new UserPathStep(new UserEnvironment(), new[] { Msys2ToolchainStep.Ucrt64Bin });
    }

    protected SetupRepairAction CreateRepairAction(string suffix, string humanMessage)
    {
        return new SetupRepairAction($"{Id}-{suffix}", humanMessage, $"{Id}: repair over {string.Join(", ", _steps.Select(step => step.Id))}");
    }

    private SetupNodeResult BuildDetectedResult(IReadOnlyList<StepExecution> executions)
    {
        var status = EvaluateDetectedStatus(executions);
        var humanMessage = status switch
        {
            SetupNodeStatus.Ready => ReadyHumanMessage,
            SetupNodeStatus.RepairRequired => RepairHumanMessage,
            _ => PendingHumanMessage,
        };

        return new SetupNodeResult(Id, Name, status, humanMessage, BuildTechnicalMessage(executions), executions);
    }

    private SetupNodeResult BuildVerifiedResult(IReadOnlyList<StepExecution> executions)
    {
        var status = EvaluateVerifiedStatus(executions);
        var humanMessage = status switch
        {
            SetupNodeStatus.Ready => ReadyHumanMessage,
            SetupNodeStatus.RepairRequired => RepairHumanMessage,
            _ => PendingHumanMessage,
        };

        return new SetupNodeResult(Id, Name, status, humanMessage, BuildTechnicalMessage(executions), executions);
    }

    private SetupNodeResult BuildMutationResult(IReadOnlyList<StepExecution> executions, string successHumanMessage)
    {
        var status = executions.All(execution => execution.Result.Success)
            ? SetupNodeStatus.Ready
            : SetupNodeStatus.Failed;
        var humanMessage = status == SetupNodeStatus.Ready
            ? successHumanMessage
            : $"No pude completar {Name.ToLowerInvariant()}.";
        return new SetupNodeResult(Id, Name, status, humanMessage, BuildTechnicalMessage(executions), executions);
    }

    private async Task<IReadOnlyList<StepExecution>> ExecuteAsync(
        string phase,
        SetupContext context,
        Func<ISetupStep, SetupContext, CancellationToken, Task<StepResult>> action,
        CancellationToken cancellationToken)
    {
        var executions = new List<StepExecution>();
        foreach (var step in _steps)
        {
            var result = await action(step, context, cancellationToken);
            executions.Add(new StepExecution(step.Id, phase, result));
        }

        return executions;
    }

    private async Task<StepResult> RunMutationAsync(
        ISetupStep step,
        string phase,
        SetupContext context,
        CancellationToken cancellationToken)
    {
        return phase switch
        {
            "install" => await step.InstallAsync(context, cancellationToken),
            "update" => await step.UpdateAsync(context, cancellationToken),
            "repair" => await step.RepairAsync(context, cancellationToken),
            _ => throw new InvalidOperationException($"Fase mutante no soportada: {phase}"),
        };
    }

    private ISetupStep FindStep(string stepId)
    {
        return _steps.First(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool NeedsApply(StepExecution execution)
    {
        return execution.Result.IsWarning || execution.Result.IsMissing;
    }

    private static SetupNodeStatus EvaluateDetectedStatus(IEnumerable<StepExecution> executions)
    {
        var executionList = executions.ToArray();
        if (executionList.All(execution => execution.Result.Success && !execution.Result.IsWarning))
        {
            return SetupNodeStatus.Ready;
        }

        if (executionList.Any(execution => !execution.Result.Success && !execution.Result.IsMissing))
        {
            return SetupNodeStatus.RepairRequired;
        }

        return SetupNodeStatus.ActionRequired;
    }

    private static SetupNodeStatus EvaluateVerifiedStatus(IEnumerable<StepExecution> executions)
    {
        var executionList = executions.ToArray();
        if (executionList.All(execution => execution.Result.Success))
        {
            return SetupNodeStatus.Ready;
        }

        if (executionList.Any(execution => !execution.Result.Success && !execution.Result.IsMissing))
        {
            return SetupNodeStatus.RepairRequired;
        }

        return SetupNodeStatus.ActionRequired;
    }

    private static string BuildTechnicalMessage(IEnumerable<StepExecution> executions)
    {
        return string.Join(
            "; ",
            executions.Select(execution => $"{execution.StepId}.{execution.Phase}: {execution.Result.Message}"));
    }
}