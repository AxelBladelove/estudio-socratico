using Estudio.Setup.Runtime;

namespace Estudio.Setup.Tests;

public class EnvironmentGeminiRuntimeConfigProviderTests
{
    [Fact]
    public async Task LoadAsync_builds_runtime_config_from_environment_api_key()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GEMINI_API_KEY"] = "AIza-env",
        };
        var provider = new EnvironmentGeminiRuntimeConfigProvider(values.GetValueOrDefault);

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.NotNull(source);
        Assert.Equal("shared", source.Gemini.Mode);
        Assert.Equal("gemini-2.5-flash", source.Gemini.Model);
        Assert.Equal("parts", source.Gemini.KeyEncoding);
        Assert.Equal(new[] { "AIza-env" }, source.Gemini.KeyParts);
        Assert.Equal("gist", source.Content.Provider);
        Assert.Equal("bundled-vsix", source.Content.CatalogSource);
    }

    [Fact]
    public async Task LoadAsync_returns_null_when_environment_api_key_is_missing()
    {
        var provider = new EnvironmentGeminiRuntimeConfigProvider(_ => null);

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.Null(source);
    }
}
