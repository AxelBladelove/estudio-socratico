namespace Estudio.Setup.Runtime;

public interface IGeminiRuntimeConfigProvider
{
    Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken);
}
