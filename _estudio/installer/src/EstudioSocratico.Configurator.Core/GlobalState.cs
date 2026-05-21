using System.Text.Json.Serialization;

namespace EstudioSocratico.Configurator.Core;

/// <summary>
/// Derived global state of the entire environment.
/// Calculated from individual dependency states and auth status.
/// </summary>
public enum GlobalState
{
    Analyzing,
    NeedsSetup,
    NeedsRepair,
    NeedsAuthentication,
    NeedsUserAction,
    ReadyToConfigure,
    Configuring,
    PartiallyReady,
    ReadyToStudy,
    Failed
}

/// <summary>
/// Extended resource status for the UI layer.
/// </summary>
public enum ResourceStatus
{
    Ready,
    Missing,
    Broken,
    Outdated,
    NeedsAuth,
    NeedsUserAction,
    Optional,
    Skipped,
    Installing,
    Repairing,
    Failed
}

/// <summary>
/// Human-readable state for a single resource shown in the UI.
/// </summary>
public sealed record ResourceState
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required ResourceStatus Status { get; init; }
    public required string Category { get; init; }
    public required bool IsCritical { get; init; }
    public string? Version { get; init; }
    public string? Path { get; init; }
    public string? HumanStatus { get; init; }
    public string? HumanDescription { get; init; }
    public string? ActionLabel { get; init; }
    public string? ActionId { get; init; }
    public InstallerError? Error { get; init; }
}

/// <summary>
/// Calculates the honest global state from diagnosis results.
/// </summary>
public static class GlobalStateCalculator
{
    /// <summary>
    /// IDs of dependencies that MUST be Ready for ReadyToStudy.
    /// </summary>
    private static readonly HashSet<DependencyId> CriticalDependencies =
    [
        DependencyId.NodeJs,
        DependencyId.Python,
        DependencyId.Git,
        DependencyId.GitHubCli,
        DependencyId.ExercismCli,
        DependencyId.VSCode,
        DependencyId.Msys2,
        DependencyId.Gcc,
        DependencyId.Make
    ];

    /// <summary>
    /// Optional dependencies. Their absence does not prevent ReadyToStudy.
    /// </summary>
    private static readonly HashSet<DependencyId> OptionalDependencies =
    [
        DependencyId.Winget
    ];

    public static GlobalState Calculate(
        IReadOnlyList<DependencyState> dependencies,
        AccountState? github = null,
        AccountState? exercism = null,
        bool workspaceValid = false,
        bool buildFlowValid = false)
    {
        var criticalMissing = new List<DependencyState>();
        var criticalBroken = new List<DependencyState>();
        var criticalOutdated = new List<DependencyState>();
        var criticalFailed = new List<DependencyState>();

        foreach (var dep in dependencies)
        {
            if (!CriticalDependencies.Contains(dep.Id))
                continue;

            switch (dep.Status)
            {
                case DependencyStatus.Missing:
                    criticalMissing.Add(dep);
                    break;
                case DependencyStatus.Broken:
                    criticalBroken.Add(dep);
                    break;
                case DependencyStatus.Outdated:
                    criticalOutdated.Add(dep);
                    break;
                case DependencyStatus.Failed:
                    criticalFailed.Add(dep);
                    break;
            }
        }

        // Failed state takes priority
        if (criticalFailed.Count > 0)
            return GlobalState.Failed;

        // Missing critical dependencies
        if (criticalMissing.Count > 0)
            return GlobalState.NeedsSetup;

        // Broken critical dependencies
        if (criticalBroken.Count > 0)
            return GlobalState.NeedsRepair;

        // Outdated critical dependencies
        if (criticalOutdated.Count > 0)
            return GlobalState.NeedsRepair;

        // Authentication requirements
        var needsGitHubAuth = github is null || !github.Configured;
        var needsExercismAuth = exercism is null || !exercism.Configured;

        if (needsGitHubAuth || needsExercismAuth)
            return GlobalState.NeedsAuthentication;

        // All criticals are Ready and auth is configured
        var allCriticalsReady = dependencies
            .Where(d => CriticalDependencies.Contains(d.Id))
            .All(d => d.Status == DependencyStatus.Ready);

        if (!allCriticalsReady)
            return GlobalState.NeedsRepair;

        // Check workspace and build flow
        if (!workspaceValid || !buildFlowValid)
            return GlobalState.NeedsUserAction;

        // Check optional dependencies
        var optionalMissing = dependencies
            .Where(d => OptionalDependencies.Contains(d.Id))
            .Any(d => d.Status != DependencyStatus.Ready && d.Status != DependencyStatus.Skipped);

        if (optionalMissing)
            return GlobalState.PartiallyReady;

        return GlobalState.ReadyToStudy;
    }

    public static string GetHumanMessage(GlobalState state) => state switch
    {
        GlobalState.Analyzing => "Revisando tu entorno...",
        GlobalState.NeedsSetup => "Faltan herramientas para empezar.",
        GlobalState.NeedsRepair => "Hay componentes que necesitan reparación.",
        GlobalState.NeedsAuthentication => "Necesitamos conectar tus cuentas.",
        GlobalState.NeedsUserAction => "Necesitamos información tuya para continuar.",
        GlobalState.ReadyToConfigure => "Tu plan de configuración está listo.",
        GlobalState.Configuring => "Configurando tu entorno de estudio...",
        GlobalState.PartiallyReady => "Puedes empezar, pero faltan pasos recomendados.",
        GlobalState.ReadyToStudy => "Todo está listo para estudiar C.",
        GlobalState.Failed => "No pudimos completar la configuración.",
        _ => "Estado desconocido."
    };

    public static FinalReadinessCheck CreateFinalReadinessCheck(
        IReadOnlyList<DependencyState> dependencies,
        AccountState? github = null,
        AccountState? exercism = null,
        bool workspaceValid = false,
        bool buildFlowValid = false,
        string? alias = null,
        string? workspacePath = null,
        string? smokeTestStatus = null)
    {
        var globalState = Calculate(dependencies, github, exercism, workspaceValid, buildFlowValid);
        var missingRequirements = dependencies
            .Where(dep => CriticalDependencies.Contains(dep.Id) && dep.Status == DependencyStatus.Missing)
            .Select(dep => dep.DisplayName)
            .ToList();
        var failedRequirements = dependencies
            .Where(dep => CriticalDependencies.Contains(dep.Id) &&
                          dep.Status is DependencyStatus.Broken or DependencyStatus.Outdated or DependencyStatus.Failed)
            .Select(dep => dep.DisplayName)
            .ToList();
        var authRequirements = new List<string>();

        if (github is null || !github.Configured)
        {
            authRequirements.Add("GitHub");
        }

        if (exercism is null || !exercism.Configured)
        {
            authRequirements.Add("Exercism");
        }

        if (!workspaceValid)
        {
            missingRequirements.Add("Workspace");
        }

        var pendingRequirements = new List<string>();
        pendingRequirements.AddRange(missingRequirements);
        pendingRequirements.AddRange(failedRequirements);
        pendingRequirements.AddRange(authRequirements);
        if (!buildFlowValid)
        {
            pendingRequirements.Add("Smoke test F9");
        }

        return new FinalReadinessCheck
        {
            GlobalState = globalState,
            GlobalMessage = GetHumanMessage(globalState),
            MissingRequirements = missingRequirements,
            FailedRequirements = failedRequirements,
            AuthRequirements = authRequirements,
            PendingRequirements = pendingRequirements,
            SmokeTestStatus = string.IsNullOrWhiteSpace(smokeTestStatus)
                ? buildFlowValid ? "passed" : "pending"
                : smokeTestStatus,
            Alias = alias,
            GitHubLogin = github?.UserName,
            WorkspacePath = workspacePath
        };
    }

    public static ResourceStatus ToResourceStatus(DependencyStatus status) => status switch
    {
        DependencyStatus.Ready => ResourceStatus.Ready,
        DependencyStatus.Missing => ResourceStatus.Missing,
        DependencyStatus.Broken => ResourceStatus.Broken,
        DependencyStatus.Outdated => ResourceStatus.Outdated,
        DependencyStatus.NeedsAuth => ResourceStatus.NeedsAuth,
        DependencyStatus.NeedsUserAction => ResourceStatus.NeedsUserAction,
        DependencyStatus.Installing => ResourceStatus.Installing,
        DependencyStatus.Repaired => ResourceStatus.Ready,
        DependencyStatus.Skipped => ResourceStatus.Skipped,
        DependencyStatus.Failed => ResourceStatus.Failed,
        _ => ResourceStatus.Missing
    };

    public static string GetHumanStatus(ResourceStatus status) => status switch
    {
        ResourceStatus.Ready => "Listo",
        ResourceStatus.Missing => "Por instalar",
        ResourceStatus.Broken => "Por reparar",
        ResourceStatus.Outdated => "Por reparar",
        ResourceStatus.NeedsAuth => "Requiere cuenta",
        ResourceStatus.NeedsUserAction => "Requiere accion",
        ResourceStatus.Optional => "Opcional",
        ResourceStatus.Skipped => "Omitido",
        ResourceStatus.Installing => "En proceso",
        ResourceStatus.Repairing => "En proceso",
        ResourceStatus.Failed => "Error",
        _ => "Desconocido"
    };

    public static bool IsCritical(DependencyId id) => CriticalDependencies.Contains(id);
}
