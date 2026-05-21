using System.Text.Json;
using System.Text.Json.Serialization;

namespace EstudioSocratico.Configurator.Core;

/// <summary>
/// Whitelist of actions the UI is allowed to request.
/// </summary>
public enum BridgeAction
{
    DiagnoseEnvironment,
    CreateSetupPlan,
    ApplyPlan,
    ApplyWorkflow,
    CancelPlan,
    RepairComponent,
    ConfigureGithub,
    ChangeGithubAccount,
    ConfigureExercism,
    OpenExercismTokenPage,
    OpenExercismCTrack,
    ConfigureWorkspace,
    OpenVSCode,
    OpenExercisePanel,
    ReinstallVSCodeExtension,
    OpenExtensionApiKeyConfig,
    RevealExtensionApiKeyConfig,
    RevealInExplorer,
    OpenLogs,
    ExportDiagnostics,
    RunSmokeTest,
    GetCurrentState,
    ReinstallManaged,
    UninstallManaged
}

/// <summary>
/// Types of events the backend can push to the UI.
/// </summary>
public enum BridgeEventType
{
    DiagnosticStarted,
    DiagnosticUpdated,
    DiagnosticCompleted,
    PlanCreated,
    StepStarted,
    StepProgress,
    StepNeedsUserInput,
    StepSucceeded,
    StepFailed,
    StepSkipped,
    VerificationStarted,
    VerificationCompleted,
    GlobalStateChanged,
    LogUpdated,
    Error
}

/// <summary>
/// Request message from the React UI to the C# backend.
/// Received via WebView2 PostMessage.
/// </summary>
public sealed record BridgeRequest
{
    public required string Id { get; init; }
    public required BridgeAction Action { get; init; }
    public Dictionary<string, object?> Payload { get; init; } = [];
}

/// <summary>
/// Response message from C# backend to the React UI.
/// Sent via WebView2 PostWebMessageAsJson.
/// </summary>
public sealed record BridgeResponse
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public bool Ok { get; init; }
    public object? Payload { get; init; }
    public InstallerError? Error { get; init; }
}

/// <summary>
/// Event message pushed from C# backend to React UI.
/// Sent via WebView2 ExecuteScriptAsync or PostWebMessageAsJson.
/// </summary>
public sealed record BridgeEvent
{
    public required BridgeEventType Type { get; init; }
    public object? Payload { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Snapshot of the full UI state, sent on initial load and after major changes.
/// </summary>
public sealed record UIStateSnapshot
{
    public GlobalState GlobalState { get; init; } = GlobalState.Analyzing;
    public string GlobalMessage { get; init; } = "";
    public IReadOnlyList<ResourceState> Resources { get; init; } = [];
    public SetupPlan? CurrentPlan { get; init; }
    public AccountState? GitHub { get; init; }
    public AccountState? Exercism { get; init; }
    public string? WorkspacePath { get; init; }
    public string? RecommendedWorkspacePath { get; init; }
    public string? LocalAlias { get; init; }
    public bool WorkspaceValid { get; init; }
    public bool BuildFlowValid { get; init; }
    public WorkspaceContextInfo WorkspaceContext { get; init; } = new();
    public VSCodeExtensionState VSCodeExtension { get; init; } = new();
    public ExtensionApiKeyConfigState ExtensionApiKeyConfig { get; init; } = new();
    public FinalReadinessCheck FinalReadiness { get; init; } = new();
    public string ConfiguratorVersion { get; init; } = ProductInfo.Version;
}

public static class BridgeProtocol
{
    private static readonly JsonNamingPolicy BridgeNamingPolicy = JsonNamingPolicy.CamelCase;

    public static string GetRequestType(BridgeAction action) =>
        BridgeNamingPolicy.ConvertName(action.ToString());

    public static string GetResultType(BridgeAction action) =>
        $"{GetRequestType(action)}.result";

    public static BridgeRequest ParseRequest(string webMessageAsJson)
    {
        using var document = JsonDocument.Parse(webMessageAsJson);
        return ParseRequest(document.RootElement);
    }

    public static BridgeResponse CreateSuccessResponse(BridgeRequest request, object? payload) =>
        new()
        {
            Id = request.Id,
            Type = GetResultType(request.Action),
            Ok = true,
            Payload = payload
        };

    public static BridgeResponse CreateErrorResponse(string requestId, InstallerError error, BridgeAction? action = null) =>
        new()
        {
            Id = requestId,
            Type = action is { } knownAction ? GetResultType(knownAction) : "error",
            Ok = false,
            Error = error
        };

    private static BridgeRequest ParseRequest(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var innerJson = element.GetString();
            if (string.IsNullOrWhiteSpace(innerJson))
            {
                throw new InvalidOperationException("El frontend envio un mensaje vacio al backend.");
            }

            using var nestedDocument = JsonDocument.Parse(innerJson);
            return ParseRequest(nestedDocument.RootElement);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("El mensaje del frontend debe ser un objeto JSON.");
        }

        var requestId = ReadRequiredString(element, "id");
        var actionName = ReadOptionalString(element, "type") ?? ReadOptionalString(element, "action");
        if (string.IsNullOrWhiteSpace(actionName))
        {
            throw new InvalidOperationException("El mensaje del frontend no incluyo una accion valida.");
        }

        if (!Enum.TryParse<BridgeAction>(actionName, ignoreCase: true, out var action))
        {
            throw new InvalidOperationException($"La accion '{actionName}' no esta permitida.");
        }

        Dictionary<string, object?> payload = [];
        if (element.TryGetProperty("payload", out var payloadElement) &&
            payloadElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (payloadElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("El payload del mensaje debe ser un objeto JSON.");
            }

            payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(payloadElement.GetRawText(), JsonDefaults.Options) ?? [];
        }

        return new BridgeRequest
        {
            Id = requestId,
            Action = action,
            Payload = payload
        };
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        var value = ReadOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"El mensaje del frontend no incluyo '{propertyName}'.");
        }

        return value;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Null => null,
            _ => property.ToString()
        };
    }
}

public static class BridgePayload
{
    public static SetupRequest ToSetupRequest(BridgeRequest request, SetupMode defaultMode = SetupMode.Install)
    {
        return new SetupRequest
        {
            Mode = GetSetupMode(request, defaultMode),
            WorkspacePath = GetString(request, "workspacePath", "workspace"),
            LocalAlias = GetString(request, "localAlias", "alias"),
            ExercismToken = GetString(request, "exercismToken", "token"),
            AllowAggressiveCleanup = GetBool(request, "allowAggressiveCleanup", defaultValue: false),
            SkipGitHubLogin = GetBool(request, "skipGitHubLogin", defaultValue: false),
            SkipExercism = GetBool(request, "skipExercism", defaultValue: false)
        };
    }

    public static SetupMode GetSetupMode(BridgeRequest request, SetupMode defaultMode = SetupMode.Install)
    {
        return ParseSetupMode(GetString(request, "mode", "workflow"), defaultMode);
    }

    public static SetupMode ParseSetupMode(string? value, SetupMode defaultMode = SetupMode.Install)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultMode;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "setup" or "install" => SetupMode.Install,
            "repair" => SetupMode.Repair,
            "reinstall" => SetupMode.Reinstall,
            "uninstall" => SetupMode.Uninstall,
            "diagnostics" or "diagnose" or "accounts" => SetupMode.Diagnostics,
            _ when Enum.TryParse<SetupMode>(value, ignoreCase: true, out var parsed) => parsed,
            _ => defaultMode
        };
    }

    public static string? GetString(BridgeRequest request, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!request.Payload.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is JsonElement element)
            {
                var elementValue = element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.ToString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => element.GetRawText()
                };

                if (!string.IsNullOrWhiteSpace(elementValue))
                {
                    return elementValue;
                }

                continue;
            }

            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    public static bool GetBool(BridgeRequest request, string key, bool defaultValue)
    {
        if (!request.Payload.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsedElement) => parsedElement,
                _ => defaultValue
            };
        }

        return value is bool boolValue
            ? boolValue
            : bool.TryParse(value.ToString(), out var parsedValue) ? parsedValue : defaultValue;
    }
}
