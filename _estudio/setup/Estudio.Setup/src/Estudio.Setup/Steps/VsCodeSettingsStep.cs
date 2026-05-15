using System.Text.Json;
using System.Text.Json.Nodes;
using Estudio.Setup.Core;

namespace Estudio.Setup.Steps;

public sealed class VsCodeSettingsStep : ISetupStep
{
    private const string DefaultProfileKey = "terminal.integrated.defaultProfile.windows";
    private const string ProfilesKey = "terminal.integrated.profiles.windows";
    private const string PowerShellProfileName = "PowerShell 7";
    private const string PowerShellPath = "pwsh.exe";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _settingsPath;
    private readonly string _alias;
    private readonly string _configPath;
    private readonly Func<DateTimeOffset> _now;

    public VsCodeSettingsStep(
        string settingsPath,
        string alias,
        string configPath,
        Func<DateTimeOffset>? now = null)
    {
        _settingsPath = settingsPath;
        _alias = alias;
        _configPath = configPath;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public string Id => "vscode-settings";
    public string Name => "VS Code settings";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckSettingsAsync(cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteSettingsAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteSettingsAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteSettingsAsync(cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckSettingsAsync(cancellationToken);
    }

    private async Task<StepResult> WriteSettingsAsync(CancellationToken cancellationToken)
    {
        JsonObject settings;
        if (File.Exists(_settingsPath))
        {
            BackupExistingSettings();
            settings = await ReadSettingsObjectAsync(cancellationToken);
        }
        else
        {
            settings = new JsonObject();
        }

        settings[DefaultProfileKey] = PowerShellProfileName;
        var profiles = GetOrCreateObject(settings, ProfilesKey);
        profiles[PowerShellProfileName] = new JsonObject
        {
            ["path"] = PowerShellPath,
        };

        settings["estudioSocratico.alias"] = _alias;
        settings["estudioSocratico.configPath"] = _configPath;

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        await File.WriteAllTextAsync(
            _settingsPath,
            settings.ToJsonString(WriteOptions),
            cancellationToken);

        return StepResult.Ok($"VS Code: settings actualizados en {_settingsPath}.");
    }

    private async Task<StepResult> CheckSettingsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return StepResult.Missing($"VS Code: no existe settings.json en {_settingsPath}.");
        }

        JsonObject settings;
        try
        {
            settings = await ReadSettingsObjectAsync(cancellationToken);
        }
        catch (JsonException ex)
        {
            return StepResult.Fail($"VS Code: settings.json no es JSON valido. {ex.Message}");
        }

        if (!HasValue(settings, DefaultProfileKey, PowerShellProfileName)
            || !HasPowerShell7Profile(settings)
            || !HasValue(settings, "estudioSocratico.alias", _alias)
            || !HasValue(settings, "estudioSocratico.configPath", _configPath))
        {
            return StepResult.Missing("VS Code: faltan settings de Estudio Socratico o perfil PowerShell 7.");
        }

        return StepResult.Ok("VS Code: settings de Estudio Socratico listos.");
    }

    private async Task<JsonObject> ReadSettingsObjectAsync(CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(json, nodeOptions: null, documentOptions: ReadOptions)?.AsObject()
            ?? new JsonObject();
    }

    private void BackupExistingSettings()
    {
        var stamp = _now().UtcDateTime.ToString("yyyyMMddHHmmss");
        var backupPath = ResolveBackupPath(stamp);
        File.Copy(_settingsPath, backupPath, overwrite: false);
    }

    private string ResolveBackupPath(string stamp)
    {
        var backupPath = $"{_settingsPath}.{stamp}.bak";
        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        for (var index = 1; ; index++)
        {
            var candidate = $"{_settingsPath}.{stamp}.{index}.bak";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool HasValue(JsonObject settings, string key, string expected)
    {
        return settings.TryGetPropertyValue(key, out var value)
            && string.Equals(value?.GetValue<string>(), expected, StringComparison.Ordinal);
    }

    private static JsonObject GetOrCreateObject(JsonObject settings, string key)
    {
        if (settings.TryGetPropertyValue(key, out var node) && node is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        settings[key] = created;
        return created;
    }

    private static bool HasPowerShell7Profile(JsonObject settings)
    {
        if (!settings.TryGetPropertyValue(ProfilesKey, out var profilesNode)
            || profilesNode is not JsonObject profiles
            || !profiles.TryGetPropertyValue(PowerShellProfileName, out var profileNode)
            || profileNode is not JsonObject profile)
        {
            return false;
        }

        return HasValue(profile, "path", PowerShellPath);
    }
}
