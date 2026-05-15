using System.Text.Json;

namespace Estudio.Setup.Runtime;

public sealed class FileGeminiRuntimeConfigProvider : IGeminiRuntimeConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;

    public FileGeminiRuntimeConfigProvider(string path)
    {
        _path = path;
    }

    public async Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<GeminiRuntimeConfigSource>(
            stream,
            JsonOptions,
            cancellationToken);
    }
}
