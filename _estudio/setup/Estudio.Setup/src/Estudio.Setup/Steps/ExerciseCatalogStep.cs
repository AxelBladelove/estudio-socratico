using System.Text.Json;
using Estudio.Setup.Core;

namespace Estudio.Setup.Steps;

public sealed class ExerciseCatalogStep : ISetupStep
{
    private readonly string _workspaceRoot;

    public ExerciseCatalogStep(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
    }

    public string Id => "exercise-catalog";
    public string Name => "Exercise catalog";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyCatalogAsync(cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyCatalogAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyCatalogAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyCatalogAsync(cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyCatalogAsync(cancellationToken);
    }

    private async Task<StepResult> VerifyCatalogAsync(CancellationToken cancellationToken)
    {
        var catalogPath = ResolveAlejandroCatalogPath(_workspaceRoot);
        if (!File.Exists(catalogPath))
        {
            return StepResult.Missing($"Catalogo: no existe {catalogPath}.");
        }

        try
        {
            await using var stream = File.OpenRead(catalogPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("exercises", out var exercises)
                || exercises.ValueKind != JsonValueKind.Array)
            {
                return StepResult.Fail("Catalogo: alejandro.json no contiene arreglo exercises.");
            }

            var count = 0;
            foreach (var exercise in exercises.EnumerateArray())
            {
                count++;
                if (!HasNonEmptyString(exercise, "slug") || !HasNonEmptyString(exercise, "title"))
                {
                    return StepResult.Fail("Catalogo: cada ejercicio necesita slug y title.");
                }

                if (!HasNonEmptyString(exercise, "gistInstructionsUrl")
                    && !HasNonEmptyString(exercise, "instructionMarkdown")
                    && !HasNonEmptyString(exercise, "driveFileId"))
                {
                    return StepResult.Fail("Catalogo: cada ejercicio Alejandro necesita gistInstructionsUrl, instructionMarkdown o driveFileId.");
                }
            }

            if (count == 0)
            {
                return StepResult.Fail("Catalogo: alejandro.json no contiene ejercicios.");
            }

            return StepResult.Ok($"Catalogo: {count} ejercicios Alejandro disponibles.");
        }
        catch (JsonException ex)
        {
            return StepResult.Fail($"Catalogo: alejandro.json no es JSON valido. {ex.Message}");
        }
        catch (IOException ex)
        {
            return StepResult.Fail($"Catalogo: no se pudo leer alejandro.json. {ex.Message}");
        }
    }

    public static string ResolveAlejandroCatalogPath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, "_estudio", "soporte", "exercism", "catalogs", "alejandro.json");
    }

    private static bool HasNonEmptyString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString());
    }
}
