using Estudio.Setup.Core;
using Estudio.Setup.Security;
using Estudio.Setup.State;

namespace Estudio.Setup.Tests;

public sealed class SensitiveDataRedactionTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-redaction-{Guid.NewGuid():N}");

    [Fact]
    public void Redact_masks_json_api_keys_and_exercism_tokens()
    {
        var text = "{\"apiKey\":\"test-runtime-key\"} exercism configure --token token-1234567890abcdefghijklmnop";

        var redacted = SensitiveDataRedactor.Redact(text);

        Assert.DoesNotContain("test-runtime-key", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("token-1234567890abcdefghijklmnop", redacted, StringComparison.Ordinal);
        Assert.Contains("API_KEY_REDACTED", redacted, StringComparison.Ordinal);
        Assert.Contains("EXERCISM_TOKEN_REDACTED", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_redacts_secret_like_values_from_setup_log()
    {
        var writer = new FileSetupLogWriter(_tempRoot, () => new DateTimeOffset(2025, 5, 5, 10, 0, 0, TimeSpan.Zero));
        var report = new SetupReport(
            Success: false,
            LastSuccessfulStep: "github-auth",
            Steps:
            [
                new StepExecution(
                    "exercism-c-track",
                    "apply",
                    StepResult.Fail("exercism configure --token token-1234567890abcdefghijklmnop")),
            ]);

        var path = await writer.SaveAsync(new SetupOptions(SetupMode.Install), "axel", report, CancellationToken.None);
        var text = await File.ReadAllTextAsync(path);

        Assert.DoesNotContain("token-1234567890abcdefghijklmnop", text, StringComparison.Ordinal);
        Assert.Contains("EXERCISM_TOKEN_REDACTED", text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}