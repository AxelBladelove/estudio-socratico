using System.Text.Json;

namespace Estudio.Setup.Runtime;

public static class RuntimeConfigMaterializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string ToLocalJson(GeminiRuntimeConfigSource source)
    {
        if (source.Gemini is null)
        {
            throw new InvalidOperationException("Runtime config invalida: falta gemini.");
        }

        if (source.Content is null)
        {
            throw new InvalidOperationException("Runtime config invalida: falta content.");
        }

        var apiKey = source.Gemini.KeyEncoding switch
        {
            "parts" => string.Concat(source.Gemini.KeyParts),
            _ => throw new InvalidOperationException($"keyEncoding no soportado: {source.Gemini.KeyEncoding}"),
        };

        RequireNonEmpty(apiKey, "gemini.apiKey");
        RequireNonEmpty(source.Gemini.Mode, "gemini.mode");
        RequireNonEmpty(source.Gemini.Model, "gemini.model");
        RequireNonEmpty(source.Content.Provider, "content.provider");
        RequireNonEmpty(source.Content.CatalogSource, "content.catalogSource");

        var localConfig = new
        {
            gemini = new
            {
                mode = source.Gemini.Mode,
                apiKey,
                model = source.Gemini.Model,
            },
            content = new
            {
                provider = source.Content.Provider,
                catalogSource = source.Content.CatalogSource,
            },
        };

        return JsonSerializer.Serialize(localConfig, JsonOptions);
    }

    private static void RequireNonEmpty(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Runtime config invalida: falta {name}.");
        }
    }
}
