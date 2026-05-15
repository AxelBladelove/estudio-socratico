using System.Text.Json;

namespace Estudio.Setup.Runtime;

public sealed class BootstrapGeminiRuntimeConfigProvider : IGeminiRuntimeConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _manifestPath;
    private readonly HttpClient _httpClient;

    public BootstrapGeminiRuntimeConfigProvider(string manifestPath, HttpClient? httpClient = null)
    {
        _manifestPath = manifestPath;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<RuntimeConfigBootstrapManifest>(
            stream,
            JsonOptions,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(manifest?.RuntimeConfigUrl)
            || !Uri.TryCreate(manifest.RuntimeConfigUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidDataException("Bootstrap runtime config invalido: falta runtimeConfigUrl absoluto.");
        }

        return await new HttpGeminiRuntimeConfigProvider(uri, _httpClient).LoadAsync(cancellationToken);
    }

    private sealed record RuntimeConfigBootstrapManifest(string RuntimeConfigUrl);
}
