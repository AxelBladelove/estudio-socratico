using Estudio.Setup.Runtime;

namespace Estudio.Setup.Tests;

public class CompositeGeminiRuntimeConfigProviderTests
{
    [Fact]
    public async Task LoadAsync_returns_first_available_runtime_config()
    {
        var expected = Source("shared");
        var provider = new CompositeGeminiRuntimeConfigProvider(
            new StaticRuntimeConfigProvider(null),
            new StaticRuntimeConfigProvider(expected),
            new StaticRuntimeConfigProvider(Source("fallback")));

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.Same(expected, source);
    }

    [Fact]
    public async Task LoadAsync_returns_null_when_no_provider_has_config()
    {
        var provider = new CompositeGeminiRuntimeConfigProvider(
            new StaticRuntimeConfigProvider(null),
            new StaticRuntimeConfigProvider(null));

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.Null(source);
    }

    private static GeminiRuntimeConfigSource Source(string mode)
    {
        return new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection(mode, "gemini-2.5-flash", "parts", new[] { "test-key-", "123" }),
            new ContentRuntimeSection("gist", "bundled-vsix"));
    }

    private sealed class StaticRuntimeConfigProvider : IGeminiRuntimeConfigProvider
    {
        private readonly GeminiRuntimeConfigSource? _source;

        public StaticRuntimeConfigProvider(GeminiRuntimeConfigSource? source)
        {
            _source = source;
        }

        public Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_source);
        }
    }
}
