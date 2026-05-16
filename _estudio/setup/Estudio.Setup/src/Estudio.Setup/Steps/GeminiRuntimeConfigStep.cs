using System.Text.Json;
using Estudio.Setup.Core;
using Estudio.Setup.Runtime;

namespace Estudio.Setup.Steps;

public sealed class GeminiRuntimeConfigStep : ISetupStep, INonBlockingSetupStep, IUninstallSetupStep
{
    private readonly string _configPath;
    private readonly IGeminiRuntimeConfigProvider _provider;

    public GeminiRuntimeConfigStep(string configPath, IGeminiRuntimeConfigProvider provider)
    {
        _configPath = configPath;
        _provider = provider;
    }

    public string Id => "gemini-runtime-config";
    public string Name => "Gemini runtime config";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckConfigAsync(cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteConfigAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteConfigAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteConfigAsync(cancellationToken);
    }

    public Task<StepResult> UninstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            return Task.FromResult(StepResult.Warning($"Gemini: config local ya no existe en {_configPath}."));
        }

        File.Delete(_configPath);
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory)
            && Directory.Exists(directory)
            && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }

        return Task.FromResult(StepResult.Ok($"Gemini: config local eliminada de {_configPath}."));
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckConfigAsync(cancellationToken);
    }

    private async Task<StepResult> WriteConfigAsync(CancellationToken cancellationToken)
    {
        GeminiRuntimeConfigSource? source;
        try
        {
            source = await _provider.LoadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException
            or JsonException
            or InvalidDataException
            or HttpRequestException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StepResult.Fail($"Gemini: no se pudo leer runtime config. {ex.Message}");
        }

        if (source is null)
        {
            return StepResult.Fail("Gemini: no se pudo obtener runtime config.");
        }

        string json;
        try
        {
            json = RuntimeConfigMaterializer.ToLocalJson(source);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail($"Gemini: runtime config invalida. {ex.Message}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        await File.WriteAllTextAsync(_configPath, json, cancellationToken);

        return StepResult.Ok($"Gemini: config local escrita en {_configPath}.");
    }

    private async Task<StepResult> CheckConfigAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            return StepResult.Missing($"Gemini: no existe config local en {_configPath}.");
        }

        try
        {
            await using var stream = File.OpenRead(_configPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("gemini", out var gemini))
            {
                return StepResult.Fail("Gemini: config local existe, pero no contiene gemini.");
            }

            if (!HasNonEmptyString(gemini, "apiKey"))
            {
                return StepResult.Fail("Gemini: config local existe, pero no contiene gemini.apiKey.");
            }

            if (!HasNonEmptyString(gemini, "model"))
            {
                return StepResult.Fail("Gemini: config local existe, pero no contiene gemini.model.");
            }

            if (!document.RootElement.TryGetProperty("content", out var content))
            {
                return StepResult.Fail("Gemini: config local existe, pero no contiene content.");
            }

            if (!HasNonEmptyString(content, "provider"))
            {
                return StepResult.Fail("Gemini: config local existe, pero no contiene content.provider.");
            }

            if (!HasNonEmptyString(content, "catalogSource"))
            {
                return StepResult.Fail("Gemini: config local existe, pero no contiene content.catalogSource.");
            }
        }
        catch (JsonException ex)
        {
            return StepResult.Fail($"Gemini: config local no es JSON valido. {ex.Message}");
        }

        return StepResult.Ok($"Gemini: config local lista en {_configPath}.");
    }

    private static bool HasNonEmptyString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString());
    }
}
