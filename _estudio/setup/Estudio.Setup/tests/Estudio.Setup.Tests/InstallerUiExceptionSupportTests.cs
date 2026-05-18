using Estudio.Setup.Windows;

namespace Estudio.Setup.Tests;

public sealed class InstallerUiExceptionSupportTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-ui-errors-{Guid.NewGuid():N}");

    [Fact]
    public async Task TryRunAsync_returns_false_and_surfaces_human_error_when_action_throws()
    {
        InstallerUiErrorState? captured = null;

        var succeeded = await InstallerUiExceptionSupport.TryRunAsync(
            () => throw new InvalidOperationException("quick review exploded"),
            exception => InstallerUiExceptionSupport.CreateInitialReviewFailure(exception, _tempRoot),
            state =>
            {
                captured = state;
                return Task.CompletedTask;
            });

        Assert.False(succeeded);
        Assert.NotNull(captured);
        Assert.Equal("No pude continuar con la revisión inicial.", captured!.Headline);
        Assert.Equal("Puedes intentar de nuevo o abrir los detalles técnicos.", captured.Body);
        Assert.True(captured.DetailsHiddenByDefault);
    }

    [Fact]
    public void CreateInitialReviewFailure_redacts_secret_like_values_and_writes_local_log()
    {
        var state = InstallerUiExceptionSupport.CreateInitialReviewFailure(
            new InvalidOperationException("exercism configure --token token-1234567890abcdefghijklmnop"),
            _tempRoot,
            () => new DateTimeOffset(2026, 5, 17, 23, 50, 0, TimeSpan.Zero));

        Assert.DoesNotContain("token-1234567890abcdefghijklmnop", state.TechnicalDetails, StringComparison.Ordinal);
        Assert.Contains("EXERCISM_TOKEN_REDACTED", state.TechnicalDetails, StringComparison.Ordinal);
        Assert.True(File.Exists(state.LogPath));
        var logText = File.ReadAllText(state.LogPath);
        Assert.DoesNotContain("token-1234567890abcdefghijklmnop", logText, StringComparison.Ordinal);
        Assert.Contains("EXERCISM_TOKEN_REDACTED", logText, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}