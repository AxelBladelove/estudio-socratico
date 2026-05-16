using Estudio.Setup.Core;
using Estudio.Setup.Tui;

namespace Estudio.Setup.Tests;

public sealed class SetupTuiPresenterTests
{
    [Fact]
    public void Snapshot_formats_components_progress_and_recent_log_lines()
    {
        var model = new SetupTuiProgressModel(new[] { "git", "node" });
        model.ApplyExecution(new StepExecution("git", "detect", StepResult.Ok("git version 2.50.0")));
        model.ApplyExecution(new StepExecution("node", "detect", StepResult.Missing("node no esta instalado")));

        var snapshot = SetupTuiPresenter.CreateSnapshot(model);

        Assert.Equal("Progreso 1/2 | OK 1 | Avisos 0 | Fallos 1", snapshot.ProgressText);
        Assert.Equal(
            new[]
            {
                "OK    git                           git version 2.50.0",
                "MISS  node                          node no esta instalado",
            },
            snapshot.ComponentLines);
        Assert.Equal(
            new[]
            {
                "git.detect: git version 2.50.0",
                "node.detect: node no esta instalado",
            },
            snapshot.LogLines);
    }

    [Fact]
    public void CreateSnapshot_limits_log_lines_to_keep_terminal_readable()
    {
        var model = new SetupTuiProgressModel(new[] { "git" });
        for (var i = 0; i < 25; i++)
        {
            model.ApplyExecution(new StepExecution("git", $"phase{i}", StepResult.Ok($"line {i}")));
        }

        var snapshot = SetupTuiPresenter.CreateSnapshot(model, maxLogLines: 3);

        Assert.Equal(
            new[]
            {
                "git.phase22: line 22",
                "git.phase23: line 23",
                "git.phase24: line 24",
            },
            snapshot.LogLines);
    }
}
