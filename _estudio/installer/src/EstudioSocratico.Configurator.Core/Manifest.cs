using System.Text.Json;
using System.Text.Json.Serialization;

namespace EstudioSocratico.Configurator.Core;

public sealed record InstallerManifest
{
    public string ConfiguratorVersion { get; init; } = ProductInfo.Version;
    public DateTimeOffset InstalledAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string WindowsUser { get; init; } = Environment.UserName;
    public string? MachineName { get; init; } = Environment.MachineName;
    public Dictionary<DependencyId, DependencyManifestEntry> Dependencies { get; init; } = [];
    public List<PathChange> PathChanges { get; init; } = [];
    public string? WorkspacePath { get; init; }
    public string? LocalAlias { get; init; }
    public bool BuildFlowValidated { get; init; }
    public DateTimeOffset? BuildFlowValidatedAtUtc { get; init; }
    public List<string> VSCodeSettingsApplied { get; init; } = [];
    public List<string> VSCodeExtensionsInstalled { get; init; } = [];
    public AccountState GitHub { get; init; } = new();
    public AccountState Exercism { get; init; } = new();
    public List<string> Logs { get; init; } = [];
    public List<ElevatedActionRecord> ElevatedActions { get; init; } = [];
    public List<string> SafeToRemove { get; init; } = [];

    [JsonIgnore]
    public bool CanUninstallSafely => Dependencies.Values.Any(x => x.InstalledByEstudio) ||
                                      SafeToRemove.Count > 0 ||
                                      !string.IsNullOrWhiteSpace(WorkspacePath);
}

public sealed record DependencyManifestEntry
{
    public DependencyId Id { get; init; }
    public string DisplayName { get; init; } = "";
    public string? VersionBefore { get; init; }
    public string? VersionAfter { get; init; }
    public string? PathBefore { get; init; }
    public string? PathAfter { get; init; }
    public bool ExistedBefore { get; init; }
    public bool InstalledByEstudio { get; init; }
    public string? InstallerSource { get; init; }
    public string? Sha256 { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PathChange
{
    public string Scope { get; init; } = "User";
    public string? Before { get; init; }
    public string? After { get; init; }
    public DateTimeOffset ChangedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AccountState
{
    public bool Configured { get; init; }
    public string? UserName { get; init; }
    public string? Host { get; init; }
    public DateTimeOffset? ValidatedAtUtc { get; init; }
    public string? StorageWarning { get; init; }
}

public sealed record ElevatedActionRecord
{
    public ElevatedOperationCode Operation { get; init; }
    public DateTimeOffset ExecutedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool Succeeded { get; init; }
    public string? Details { get; init; }
}

public sealed record DiagnosticsReport
{
    public string ConfiguratorVersion { get; init; } = ProductInfo.Version;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string OperatingSystem { get; init; } = System.Environment.OSVersion.VersionString;
    public string WindowsUser { get; init; } = System.Environment.UserName;
    public string? WorkspacePath { get; init; }
    public IReadOnlyList<DependencyState> Dependencies { get; init; } = Array.Empty<DependencyState>();
    public IReadOnlyList<InstallerError> Errors { get; init; } = Array.Empty<InstallerError>();
    public Dictionary<string, string> Environment { get; init; } = [];
}

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
