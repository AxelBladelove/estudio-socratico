namespace Estudio.Setup.Runtime;

public sealed class CompositeGeminiRuntimeConfigProvider : IGeminiRuntimeConfigProvider
{
    private readonly IReadOnlyList<IGeminiRuntimeConfigProvider> _providers;

    public CompositeGeminiRuntimeConfigProvider(params IGeminiRuntimeConfigProvider[] providers)
    {
        _providers = providers;
    }

    public async Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in _providers)
        {
            var source = await provider.LoadAsync(cancellationToken);
            if (source is not null)
            {
                return source;
            }
        }

        return null;
    }
}
