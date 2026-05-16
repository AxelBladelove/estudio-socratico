using System.Net;
using Estudio.Setup.Runtime;

namespace Estudio.Setup.Tests;

public class HttpGeminiRuntimeConfigProviderTests
{
    [Fact]
    public async Task LoadAsync_reads_runtime_config_json_from_http_url()
    {
        var provider = new HttpGeminiRuntimeConfigProvider(
            new Uri("https://gist.githubusercontent.com/example/runtime-config.json"),
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "gemini": {
                        "mode": "shared",
                        "model": "gemini-2.5-flash",
                        "keyEncoding": "parts",
                        "keyParts": ["test-key-", "remote"]
                      },
                      "content": {
                        "provider": "gist",
                        "catalogSource": "bundled-vsix"
                      }
                    }
                    """),
            })));

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.NotNull(source);
        Assert.Equal("gemini-2.5-flash", source.Gemini.Model);
        Assert.Equal(new[] { "test-key-", "remote" }, source.Gemini.KeyParts);
    }

    [Fact]
    public async Task LoadAsync_throws_invalid_data_when_server_rejects_download()
    {
        var provider = new HttpGeminiRuntimeConfigProvider(
            new Uri("https://gist.githubusercontent.com/example/runtime-config.json"),
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("missing"),
            })));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => provider.LoadAsync(CancellationToken.None));

        Assert.Contains("404", ex.Message);
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
