using System.Net;
using System.Text.Json;

namespace Estudio.Setup.Runtime;

public sealed class HttpGeminiRuntimeConfigProvider : IGeminiRuntimeConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Uri _runtimeConfigUri;
    private readonly HttpClient _httpClient;

    public HttpGeminiRuntimeConfigProvider(Uri runtimeConfigUri, HttpClient httpClient)
    {
        _runtimeConfigUri = runtimeConfigUri;
        _httpClient = httpClient;
    }

    public async Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(_runtimeConfigUri, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidDataException(
                $"Runtime config HTTP devolvio {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GeminiRuntimeConfigSource>(
            stream,
            JsonOptions,
            cancellationToken);
    }
}
