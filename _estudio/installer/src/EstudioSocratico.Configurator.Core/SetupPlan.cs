namespace EstudioSocratico.Configurator.Core;

/// <summary>
/// Severity of a setup action.
/// </summary>
public enum ActionSeverity
{
    Critical,
    Recommended,
    Optional
}

/// <summary>
/// Category of a setup action for grouping in the UI.
/// </summary>
public enum ActionCategory
{
    SystemTools,
    Authentication,
    Workspace,
    Validation
}

/// <summary>
/// A single action in the setup plan.
/// </summary>
public sealed record SetupAction
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public ActionCategory Category { get; init; } = ActionCategory.SystemTools;
    public ActionSeverity Severity { get; init; } = ActionSeverity.Critical;
    public bool RequiresAdmin { get; init; }
    public bool RequiresUser { get; init; }
    public bool CanSkip { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = [];
    public DependencyId? DependencyId { get; init; }
    public string? InternalCommand { get; init; }
    public string? RollbackNotes { get; init; }
    public string? EvidenceExpected { get; init; }

    // Mutable state during execution
    public DependencyStatus Status { get; set; } = DependencyStatus.Unknown;
    public string? StatusMessage { get; set; }
    public double ProgressPercent { get; set; }
}

/// <summary>
/// A plan of actions to configure the environment.
/// Generated from diagnosis, shown to user before applying.
/// </summary>
public sealed record SetupPlan
{
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<SetupAction> Actions { get; init; } = [];
    public int TotalActions => Actions.Count;
    public int CriticalActions => Actions.Count(a => a.Severity == ActionSeverity.Critical);
    public int CompletedActions => Actions.Count(a => a.Status == DependencyStatus.Ready);

    /// <summary>
    /// Human-readable summary of the plan.
    /// </summary>
    public string Summary => TotalActions switch
    {
        0 => "No se requieren acciones.",
        1 => "Se realizará 1 acción.",
        _ => $"Se realizarán {TotalActions} acciones."
    };
}
