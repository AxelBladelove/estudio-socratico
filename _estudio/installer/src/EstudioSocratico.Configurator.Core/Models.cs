using System.Text.Json.Serialization;
using System.Text;

namespace EstudioSocratico.Configurator.Core;

public enum SetupMode
{
    Install,
    Repair,
    Reinstall,
    Uninstall,
    Diagnostics
}

public enum DependencyId
{
    Winget,
    NodeJs,
    Python,
    Git,
    GitHubCli,
    ExercismCli,
    VSCode,
    Msys2,
    Gcc,
    Make
}

public enum DependencyStatus
{
    Unknown,
    Ready,
    Missing,
    Broken,
    Outdated,
    NeedsAuth,
    NeedsUserAction,
    Installing,
    Repaired,
    Skipped,
    Failed
}

public enum InstallScope
{
    User,
    Machine
}

public enum ElevatedOperationCode
{
    InstallMsys2,
    AddMachinePath,
    InstallWingetPackage,
    RunOfficialInstaller,
    RemoveManagedDependency,
    RepairPath
}

public enum InstallerErrorCode
{
    None,
    WINGET_NOT_AVAILABLE,
    WINGET_INSTALL_FAILED,
    NETWORK_DOWNLOAD_FAILED,
    CHECKSUM_VERIFICATION_FAILED,
    MSYS2_INSTALL_FAILED,
    GCC_VALIDATION_FAILED,
    MAKE_VALIDATION_FAILED,
    GH_AUTH_FAILED,
    EXERCISM_TOKEN_INVALID,
    VSCODE_NOT_FOUND,
    WORKSPACE_LOCKED,
    PATH_UPDATE_FAILED,
    ELEVATION_REQUIRED,
    UNINSTALL_MANIFEST_MISSING,
    UNSAFE_DELETE_REQUEST,
    COMMAND_FAILED,
    UNKNOWN_ERROR
}

public sealed record DependencyRequirement(
    DependencyId Id,
    string DisplayName,
    string CommandName,
    string? WingetId,
    string? MinimumVersion,
    bool Required = true,
    bool ManagedThroughMsys2 = false);

public sealed record DependencyState
{
    public DependencyId Id { get; init; }
    public string DisplayName { get; init; } = "";
    public DependencyStatus Status { get; init; } = DependencyStatus.Unknown;
    public string? Version { get; init; }
    public string? Path { get; init; }
    public string? Source { get; init; }
    public string? Recommendation { get; init; }
    public bool InstalledByEstudio { get; init; }
    public bool RequiresElevation { get; init; }
    public InstallerError? Error { get; init; }
}

public sealed record CommandSpec
{
    public required string FileName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public string? ArgumentString { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string?> Environment { get; init; } =
        new Dictionary<string, string?>();
    public bool AllowNonZeroExitCode { get; init; }
    public bool RedactOutput { get; init; } = true;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
}

public sealed record CommandResult
{
    public required CommandSpec Spec { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public bool TimedOut { get; init; }

    [JsonIgnore]
    public bool Succeeded => !TimedOut && ExitCode == 0;
}

public sealed record InstallerError
{
    public InstallerErrorCode Code { get; init; } = InstallerErrorCode.UNKNOWN_ERROR;
    public string Title { get; init; } = "Error inesperado";
    public string Description { get; init; } = "El configurador no pudo completar la accion.";
    public string ProbableCause { get; init; } = "La causa no pudo determinarse automaticamente.";
    public string RecommendedAction { get; init; } = "Abre los logs y vuelve a intentar el paso.";
    public string? TechnicalDetails { get; init; }
    public bool CanRetry { get; init; } = true;
    public bool CanContinueSafely { get; init; }

    public static InstallerError FromException(Exception exception, InstallerErrorCode code = InstallerErrorCode.UNKNOWN_ERROR)
    {
        return new InstallerError
        {
            Code = code,
            Title = code == InstallerErrorCode.UNKNOWN_ERROR ? "Error inesperado" : code.ToString(),
            Description = exception.Message,
            ProbableCause = "Una operacion del sistema devolvio un error.",
            RecommendedAction = "Revisa el diagnostico exportado y vuelve a ejecutar Reparar.",
            TechnicalDetails = exception.ToString()
        };
    }
}

public sealed record ProgressEvent
{
    public string StepId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public double Percent { get; init; }
    public DependencyId? Dependency { get; init; }
    public DependencyStatus Status { get; init; } = DependencyStatus.Unknown;
}

public sealed record SetupRequest
{
    public SetupMode Mode { get; init; } = SetupMode.Install;
    public string? WorkspacePath { get; init; }
    public string? LocalAlias { get; init; }
    public string? ExercismToken { get; init; }
    public InstallScope Scope { get; init; } = InstallScope.User;
    public bool AllowAggressiveCleanup { get; init; }
    public bool SkipGitHubLogin { get; init; }
    public bool SkipExercism { get; init; }
}

public sealed record SetupSummary
{
    public SetupMode Mode { get; init; }
    public bool Succeeded { get; init; }
    public GlobalState GlobalState { get; init; } = GlobalState.Analyzing;
    public string GlobalMessage { get; init; } = "";
    public IReadOnlyList<DependencyState> Dependencies { get; init; } = Array.Empty<DependencyState>();
    public IReadOnlyList<InstallerError> Errors { get; init; } = Array.Empty<InstallerError>();
    public string? WorkspacePath { get; init; }
    public string? LogPath { get; init; }
    public string? DiagnosticsPath { get; init; }
    public SetupPlan? Plan { get; init; }
    public UIStateSnapshot? CurrentState { get; init; }
}

public sealed record FinalReadinessCheck
{
    public GlobalState GlobalState { get; init; } = GlobalState.Analyzing;
    public string GlobalMessage { get; init; } = "";
    public IReadOnlyList<string> MissingRequirements { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FailedRequirements { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AuthRequirements { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PendingRequirements { get; init; } = Array.Empty<string>();
    public string SmokeTestStatus { get; init; } = "unknown";
    public string? Alias { get; init; }
    public string? GitHubLogin { get; init; }
    public string? WorkspacePath { get; init; }
}

public sealed record WorkspaceContextInfo
{
    public string BaseRepo { get; init; } = ProductInfo.BaseRepository;
    public string? WorkspaceRepo { get; init; }
    public string? LocalAlias { get; init; }
    public string? GitHubLogin { get; init; }
    public string? WorkspacePath { get; init; }
    public string? RecommendedWorkspacePath { get; init; }
}

public sealed record VSCodeExtensionState
{
    public string ExtensionId { get; init; } = ProductInfo.VSCodeExtensionId;
    public ResourceStatus Status { get; init; } = ResourceStatus.NeedsUserAction;
    public string HumanStatus { get; init; } = "Requiere accion";
    public string? HumanDescription { get; init; }
    public string? SourcePath { get; init; }
    public string? InstalledPath { get; init; }
    public bool SourceExists { get; init; }
    public bool InstalledInVSCode { get; init; }
    public bool ActivityBarConfigured { get; init; }
    public bool CommandsRegistered { get; init; }
    public bool ExercisePanelAvailable { get; init; }
    public bool ManagerScriptExists { get; init; }
}

public sealed record ExtensionApiKeyConfigState
{
    public ResourceStatus Status { get; init; } = ResourceStatus.NeedsUserAction;
    public string HumanStatus { get; init; } = "Requiere accion";
    public string? HumanDescription { get; init; }
    public string? LocalConfigPath { get; init; }
    public string? ExampleConfigPath { get; init; }
    public bool LocalConfigExists { get; init; }
    public bool ExampleConfigExists { get; init; }
}

public static class LocalAliasNormalizer
{
    public static string Normalize(string? value, string? fallback = null)
    {
        var normalized = Slugify(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        normalized = Slugify(fallback);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return Slugify(Environment.UserName);
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousDash = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            var next = char.IsLetterOrDigit(ch) ? ch : '-';
            if (next == '-')
            {
                if (previousDash)
                {
                    continue;
                }

                previousDash = true;
            }
            else
            {
                previousDash = false;
            }

            builder.Append(next);
        }

        return builder.ToString().Trim('-');
    }
}

public sealed record OfficialInstallerSource
{
    public required DependencyId Dependency { get; init; }
    public required Uri Uri { get; init; }
    public string? Version { get; init; }
    public string? Sha256 { get; init; }
    public IReadOnlyList<string> SilentArguments { get; init; } = Array.Empty<string>();
    public bool RequiresElevation { get; init; }
    public string SourceName { get; init; } = "official";
}

public sealed record ElevatedOperationRequest
{
    public ElevatedOperationCode Operation { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ElevatedOperationResult
{
    public bool Succeeded { get; init; }
    public InstallerError? Error { get; init; }
    public string Message { get; init; } = "";
}
