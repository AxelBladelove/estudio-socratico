using System.Text.Json;
using Estudio.Setup.Core;

namespace Estudio.Setup.Tests;

public sealed class JsonSetupProgressSinkTests
{
    [Fact]
    public async Task ReportAsync_writes_line_delimited_json_progress_events()
    {
        using var writer = new StringWriter();
        var sink = new JsonSetupProgressSink(writer);

        await sink.ReportAsync(new SetupRunStarted(SetupMode.Install), CancellationToken.None);
        await sink.ReportAsync(new SetupPhaseStarted("git", "Git", "detect"), CancellationToken.None);
        await sink.ReportAsync(
            new SetupPhaseFinished(new StepExecution("git", "detect", StepResult.Ok("Git listo."))),
            CancellationToken.None);
        await sink.ReportAsync(
            new SetupRunFinished(SetupReport.Passed(Array.Empty<StepExecution>())),
            CancellationToken.None);

        var events = writer
            .ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();

        Assert.Equal("run-started", events[0].GetProperty("type").GetString());
        Assert.Equal("Install", events[0].GetProperty("mode").GetString());
        Assert.Equal("phase-started", events[1].GetProperty("type").GetString());
        Assert.Equal("git", events[1].GetProperty("stepId").GetString());
        Assert.Equal("phase-finished", events[2].GetProperty("type").GetString());
        Assert.Equal("ok", events[2].GetProperty("status").GetString());
        Assert.Equal("run-finished", events[3].GetProperty("type").GetString());
        Assert.True(events[3].GetProperty("success").GetBoolean());
    }
}
