using Estudio.Setup.Core;
using Estudio.Setup.State;

namespace Estudio.Setup.Tests;

public class SetupLogWriterTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "EstudioSetupTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_appends_dated_log_with_step_markers()
    {
        var writer = new FileSetupLogWriter(
            _tempRoot,
            () => new DateTimeOffset(2026, 5, 15, 10, 30, 0, TimeSpan.Zero));
        var report = SetupReport.Failed("git", new[]
        {
            new StepExecution("git", "detect", StepResult.Ok("git found")),
            new StepExecution("gemini-runtime-config", "verify", StepResult.Warning("gemini missing")),
            new StepExecution("vscode", "detect", StepResult.Missing("code missing")),
        });

        var path = await writer.SaveAsync(new SetupOptions(SetupMode.Verify), "axel", report, CancellationToken.None);

        Assert.Equal(Path.Combine(_tempRoot, "setup-2026-05-15.log"), path);
        var log = await File.ReadAllTextAsync(path);
        Assert.Contains("Estudio.Setup 2.0", log);
        Assert.Contains("Modo: Verify", log);
        Assert.Contains("Alias: axel", log);
        Assert.Contains("OK git.detect: git found", log);
        Assert.Contains("ADVERTENCIA gemini-runtime-config.verify: gemini missing", log);
        Assert.Contains("FALTA vscode.detect: code missing", log);
        Assert.Contains("Resultado: ERROR", log);
    }

    [Fact]
    public async Task SaveAsync_appends_to_existing_daily_log()
    {
        var writer = new FileSetupLogWriter(
            _tempRoot,
            () => new DateTimeOffset(2026, 5, 15, 10, 30, 0, TimeSpan.Zero));
        var report = SetupReport.Passed(Array.Empty<StepExecution>());

        var first = await writer.SaveAsync(new SetupOptions(SetupMode.Verify), "axel", report, CancellationToken.None);
        var second = await writer.SaveAsync(new SetupOptions(SetupMode.Repair), "axel", report, CancellationToken.None);

        Assert.Equal(first, second);
        var log = await File.ReadAllTextAsync(first);
        Assert.Contains("Modo: Verify", log);
        Assert.Contains("Modo: Repair", log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
