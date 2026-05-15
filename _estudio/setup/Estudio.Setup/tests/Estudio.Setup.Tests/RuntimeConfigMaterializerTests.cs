using Estudio.Setup.Runtime;

namespace Estudio.Setup.Tests;

public class RuntimeConfigMaterializerTests
{
    [Fact]
    public void ToLocalJson_reconstructs_key_parts_without_exposing_runtime_shape()
    {
        var source = new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection("shared", "gemini-2.5-flash", "parts", new[] { "AIza", "123" }),
            new ContentRuntimeSection("gist", "bundled-vsix"));

        var json = RuntimeConfigMaterializer.ToLocalJson(source);

        Assert.Contains(@"""apiKey"": ""AIza123""", json);
        Assert.Contains(@"""model"": ""gemini-2.5-flash""", json);
        Assert.Contains(@"""catalogSource"": ""bundled-vsix""", json);
        Assert.DoesNotContain("keyParts", json);
    }

    [Fact]
    public void ToLocalJson_rejects_unknown_key_encoding()
    {
        var source = new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection("shared", "gemini-2.5-flash", "plain", new[] { "AIza123" }),
            new ContentRuntimeSection("gist", "bundled-vsix"));

        var ex = Assert.Throws<InvalidOperationException>(() => RuntimeConfigMaterializer.ToLocalJson(source));

        Assert.Contains("keyEncoding", ex.Message);
    }

    [Fact]
    public void ToLocalJson_rejects_empty_reconstructed_api_key()
    {
        var source = new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection("shared", "gemini-2.5-flash", "parts", Array.Empty<string>()),
            new ContentRuntimeSection("gist", "bundled-vsix"));

        var ex = Assert.Throws<InvalidOperationException>(() => RuntimeConfigMaterializer.ToLocalJson(source));

        Assert.Contains("apiKey", ex.Message);
    }

    [Fact]
    public void ToLocalJson_rejects_missing_model()
    {
        var source = new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection("shared", string.Empty, "parts", new[] { "AIza", "123" }),
            new ContentRuntimeSection("gist", "bundled-vsix"));

        var ex = Assert.Throws<InvalidOperationException>(() => RuntimeConfigMaterializer.ToLocalJson(source));

        Assert.Contains("model", ex.Message);
    }

    [Fact]
    public void ToLocalJson_rejects_missing_gemini_section()
    {
        var source = new GeminiRuntimeConfigSource(null!, new ContentRuntimeSection("gist", "bundled-vsix"));

        var ex = Assert.Throws<InvalidOperationException>(() => RuntimeConfigMaterializer.ToLocalJson(source));

        Assert.Contains("gemini", ex.Message);
    }
}
