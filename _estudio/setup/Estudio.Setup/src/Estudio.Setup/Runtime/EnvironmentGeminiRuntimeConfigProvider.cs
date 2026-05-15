namespace Estudio.Setup.Runtime;

public sealed class EnvironmentGeminiRuntimeConfigProvider : IGeminiRuntimeConfigProvider
{
    private readonly Func<string, string?> _getEnvironmentVariable;

    public EnvironmentGeminiRuntimeConfigProvider(Func<string, string?>? getEnvironmentVariable = null)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    }

    public Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
    {
        var apiKey = _getEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult<GeminiRuntimeConfigSource?>(null);
        }

        var source = new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection(
                Mode: ValueOrDefault("GEMINI_MODE", "shared"),
                Model: ValueOrDefault("GEMINI_MODEL", "gemini-2.5-flash"),
                KeyEncoding: "parts",
                KeyParts: new[] { apiKey.Trim() }),
            new ContentRuntimeSection(
                Provider: ValueOrDefault("GEMINI_CONTENT_PROVIDER", "gist"),
                CatalogSource: ValueOrDefault("GEMINI_CATALOG_SOURCE", "bundled-vsix")));

        return Task.FromResult<GeminiRuntimeConfigSource?>(source);
    }

    private string ValueOrDefault(string name, string fallback)
    {
        var value = _getEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
