using Estudio.Setup.Core;
using Estudio.Setup.Tui;

namespace Estudio.Setup.Tests;

public class SetupTuiProgressModelTests
{
    [Fact]
    public async Task ReportAsync_tracks_component_statuses_and_log_lines()
    {
        var model = new SetupTuiProgressModel(new[] { "git", "vscode" });

        await model.ReportAsync(new SetupRunStarted(SetupMode.Verify), CancellationToken.None);
        await model.ReportAsync(new SetupPhaseStarted("git", "Git", "detect"), CancellationToken.None);
        await model.ReportAsync(new SetupPhaseFinished(new StepExecution("git", "detect", StepResult.Ok("git found"))), CancellationToken.None);
        await model.ReportAsync(new SetupPhaseFinished(new StepExecution("vscode", "detect", StepResult.Missing("code missing"))), CancellationToken.None);
        await model.ReportAsync(new SetupRunFinished(SetupReport.Failed("git", new[]
        {
            new StepExecution("git", "detect", StepResult.Ok("git found")),
            new StepExecution("vscode", "detect", StepResult.Missing("code missing")),
        })), CancellationToken.None);

        Assert.Equal("OK", model.Components.Single(component => component.Id == "git").Status);
        Assert.Equal("FALTA", model.Components.Single(component => component.Id == "vscode").Status);
        Assert.Equal(1, model.CompletedCount);
        Assert.Equal(2, model.TotalCount);
        Assert.Contains("git.detect: git found", model.LogLines);
        Assert.Contains("Resultado: ERROR", model.LogLines);
    }

    [Fact]
    public void FailedStepIds_returns_failed_or_missing_components_for_retry()
    {
        var model = new SetupTuiProgressModel(new[] { "git", "vscode", "gemini-runtime-config" });
        model.ApplyExecution(new StepExecution("git", "verify", StepResult.Ok("git ok")));
        model.ApplyExecution(new StepExecution("vscode", "detect", StepResult.Missing("missing")));
        model.ApplyExecution(new StepExecution("gemini-runtime-config", "verify", StepResult.Warning("optional")));

        Assert.Equal(new[] { "vscode" }, model.FailedStepIds());
    }
}
