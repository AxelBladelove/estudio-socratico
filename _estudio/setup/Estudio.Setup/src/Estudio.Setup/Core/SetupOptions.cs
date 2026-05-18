namespace Estudio.Setup.Core;

public sealed record SetupOptions(
    SetupMode Mode,
    string? StateRoot = null,
    string? AliasOverride = null,
    string? WorkspaceRoot = null,
    bool HelpRequested = false,
    IReadOnlyList<string>? OnlyStepIds = null,
    bool TuiRequested = false,
    bool ForceGitHubRelogin = false,
    bool JsonProgressRequested = false,
    SetupExecutionEngine Engine = SetupExecutionEngine.Legacy);
