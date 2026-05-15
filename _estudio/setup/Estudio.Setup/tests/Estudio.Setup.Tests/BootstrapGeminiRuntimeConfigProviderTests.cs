using System.Net;
using Estudio.Setup.Runtime;

namespace Estudio.Setup.Tests;

public class BootstrapGeminiRuntimeConfigProviderTests
{
    [Fact]
    public async Task LoadAsync_returns_null_when_manifest_does_not_exist()
    {
        var provider = new BootstrapGeminiRuntimeConfigProvider(
            Path.Combine(MakeTempRoot(), "runtime-config.bootstrap.json"),
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not call HTTP."))));

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.Null(source);
    }

    [Fact]
    public async Task LoadAsync_downloads_runtime_config_from_manifest_url()
    {
        var root = MakeTempRoot();
        var manifestPath = Path.Combine(root, "runtime-config.bootstrap.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            manifestPath,
            """
            {
              "runtimeConfigUrl": "https://gist.githubusercontent.com/example/runtime-config.json"
            }
            """);
        Uri? requestedUri = null;
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "gemini": {
                        "mode": "shared",
                        "model": "gemini-2.5-flash",
                        "keyEncoding": "parts",
                        "keyParts": ["AIza", "remote"]
                      },
                      "content": {
                        "provider": "gist",
                        "catalogSource": "bundled-vsix"
                      }
                    }
                    """),
            };
        }));
        var provider = new BootstrapGeminiRuntimeConfigProvider(manifestPath, httpClient);

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.NotNull(source);
        Assert.Equal(new Uri("https://gist.githubusercontent.com/example/runtime-config.json"), requestedUri);
        Assert.Equal("shared", source.Gemini.Mode);
        Assert.Equal(new[] { "AIza", "remote" }, source.Gemini.KeyParts);
        Assert.Equal("bundled-vsix", source.Content.CatalogSource);
    }

    [Theory]
    [InlineData("")]
    [InlineData("runtime-config.json")]
    public async Task LoadAsync_rejects_missing_or_relative_runtime_config_url(string runtimeConfigUrl)
    {
        var root = MakeTempRoot();
        var manifestPath = Path.Combine(root, "runtime-config.bootstrap.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            manifestPath,
            $$"""
            {
              "runtimeConfigUrl": "{{runtimeConfigUrl}}"
            }
            """);
        var provider = new BootstrapGeminiRuntimeConfigProvider(
            manifestPath,
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not call HTTP."))));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => provider.LoadAsync(CancellationToken.None));

        Assert.Contains("runtimeConfigUrl", ex.Message);
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
