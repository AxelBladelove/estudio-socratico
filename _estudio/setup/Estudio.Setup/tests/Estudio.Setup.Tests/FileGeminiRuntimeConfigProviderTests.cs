using Estudio.Setup.Runtime;

namespace Estudio.Setup.Tests;

public class FileGeminiRuntimeConfigProviderTests
{
    [Fact]
    public async Task LoadAsync_reads_runtime_config_json()
    {
        var path = Path.Combine(MakeTempRoot(), "runtime-config.private.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "gemini": {
                "mode": "shared",
                "model": "gemini-2.5-flash",
                "keyEncoding": "parts",
                "keyParts": ["AIza", "123"]
              },
              "content": {
                "provider": "gist",
                "catalogSource": "bundled-vsix"
              }
            }
            """);
        var provider = new FileGeminiRuntimeConfigProvider(path);

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.NotNull(source);
        Assert.Equal("shared", source.Gemini.Mode);
        Assert.Equal(new[] { "AIza", "123" }, source.Gemini.KeyParts);
        Assert.Equal("gist", source.Content.Provider);
    }

    [Fact]
    public async Task LoadAsync_returns_null_when_file_does_not_exist()
    {
        var provider = new FileGeminiRuntimeConfigProvider(Path.Combine(MakeTempRoot(), "missing.json"));

        var source = await provider.LoadAsync(CancellationToken.None);

        Assert.Null(source);
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }
}
