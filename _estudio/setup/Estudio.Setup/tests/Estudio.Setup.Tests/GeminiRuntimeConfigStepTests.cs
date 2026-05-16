using Estudio.Setup.Core;
using Estudio.Setup.Runtime;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class GeminiRuntimeConfigStepTests
{
    [Fact]
    public async Task InstallAsync_writes_local_config_json_with_reconstructed_key()
    {
        var configPath = Path.Combine(MakeTempRoot(), "config.json");
        var step = new GeminiRuntimeConfigStep(configPath, new FakeRuntimeConfigProvider(MakeSource()));

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        var json = await File.ReadAllTextAsync(configPath);
        Assert.Contains(@"""apiKey"": ""test-key-123""", json);
        Assert.Contains(@"""provider"": ""gist""", json);
    }

    [Fact]
    public async Task DetectAsync_returns_missing_when_config_file_does_not_exist()
    {
        var configPath = Path.Combine(MakeTempRoot(), "missing.json");
        var step = new GeminiRuntimeConfigStep(configPath, new FakeRuntimeConfigProvider(MakeSource()));

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public async Task VerifyAsync_returns_ok_when_config_has_api_key()
    {
        var configPath = Path.Combine(MakeTempRoot(), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await File.WriteAllTextAsync(configPath, RuntimeConfigMaterializer.ToLocalJson(MakeSource()));
        var step = new GeminiRuntimeConfigStep(configPath, new FakeRuntimeConfigProvider(MakeSource()));

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UninstallAsync_removes_local_runtime_config_when_present()
    {
        var configPath = Path.Combine(MakeTempRoot(), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await File.WriteAllTextAsync(configPath, "{}");
        var step = new GeminiRuntimeConfigStep(configPath, new FakeRuntimeConfigProvider(MakeSource()));

        var result = await step.UninstallAsync(new SetupContext(new SetupOptions(SetupMode.Uninstall)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(configPath));
    }

    [Fact]
    public async Task InstallAsync_returns_fail_without_writing_file_when_runtime_config_is_invalid()
    {
        var configPath = Path.Combine(MakeTempRoot(), "config.json");
        var invalidSource = new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection("shared", "gemini-2.5-flash", "parts", Array.Empty<string>()),
            new ContentRuntimeSection("gist", "bundled-vsix"));
        var step = new GeminiRuntimeConfigStep(configPath, new FakeRuntimeConfigProvider(invalidSource));

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(File.Exists(configPath));
        Assert.Contains("apiKey", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_returns_fail_when_config_is_missing_model_or_catalog_source()
    {
        var configPath = Path.Combine(MakeTempRoot(), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await File.WriteAllTextAsync(
            configPath,
            """
            {
              "gemini": { "apiKey": "test-key-123" },
              "content": { "provider": "gist" }
            }
            """);
        var step = new GeminiRuntimeConfigStep(configPath, new FakeRuntimeConfigProvider(MakeSource()));

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("model", result.Message);
    }

    [Fact]
    public async Task InstallAsync_returns_fail_when_runtime_config_provider_cannot_read_source()
    {
        var configPath = Path.Combine(MakeTempRoot(), "config.json");
        var step = new GeminiRuntimeConfigStep(
            configPath,
            new ThrowingRuntimeConfigProvider(new InvalidDataException("bad runtime config")));

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("bad runtime config", result.Message);
        Assert.False(File.Exists(configPath));
    }

    [Fact]
    public async Task InstallAsync_returns_fail_when_runtime_config_download_fails()
    {
        var configPath = Path.Combine(MakeTempRoot(), "config.json");
        var step = new GeminiRuntimeConfigStep(
            configPath,
            new ThrowingRuntimeConfigProvider(new HttpRequestException("network unavailable")));

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("network unavailable", result.Message);
        Assert.False(File.Exists(configPath));
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }

    private static GeminiRuntimeConfigSource MakeSource()
    {
        return new GeminiRuntimeConfigSource(
            new GeminiRuntimeSection("shared", "gemini-2.5-flash", "parts", new[] { "test-key-", "123" }),
            new ContentRuntimeSection("gist", "bundled-vsix"));
    }

    private sealed class FakeRuntimeConfigProvider : IGeminiRuntimeConfigProvider
    {
        private readonly GeminiRuntimeConfigSource _source;

        public FakeRuntimeConfigProvider(GeminiRuntimeConfigSource source)
        {
            _source = source;
        }

        public Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<GeminiRuntimeConfigSource?>(_source);
        }
    }

    private sealed class ThrowingRuntimeConfigProvider : IGeminiRuntimeConfigProvider
    {
        private readonly Exception _exception;

        public ThrowingRuntimeConfigProvider(Exception exception)
        {
            _exception = exception;
        }

        public Task<GeminiRuntimeConfigSource?> LoadAsync(CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }
}
