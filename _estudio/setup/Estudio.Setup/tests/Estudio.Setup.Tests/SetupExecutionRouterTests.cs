using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public sealed class SetupExecutionRouterTests
{
    [Fact]
    public async Task RunAndPersistAsync_uses_legacy_runner_by_default()
    {
        var legacyCalled = false;
        var desiredCalled = false;
        var router = new SetupExecutionRouter(
            (_, _, _, _, _, _) =>
            {
                legacyCalled = true;
                return Task.FromResult(new SetupRunArtifacts(
                    SetupReport.Passed(new[]
                    {
                        new StepExecution("git.detect", "verify", StepResult.Ok("git listo")),
                    }),
                    StatePath: "state.json",
                    LogPath: "setup.log",
                    ReportPath: "setup-report.md",
                    Alias: "axel"));
            },
            (_, _, _, _, _, _) =>
            {
                desiredCalled = true;
                return Task.FromResult(new DesiredStateSetupRunArtifacts(
                    CreateDesiredStateReport(success: true),
                    StatePath: "state.json",
                    LogPath: "setup.log",
                    ReportPath: "setup-report.md",
                    Alias: "axel"));
            });

        var artifacts = await router.RunAndPersistAsync(
            new SetupOptions(SetupMode.Verify),
            workspaceRoot: "workspace",
            studentAlias: "axel",
            new NoopCommandRunner(),
            jsonWriter: null,
            CancellationToken.None);

        Assert.True(legacyCalled);
        Assert.False(desiredCalled);
        Assert.Equal(SetupExecutionEngine.Legacy, artifacts.Engine);
    }

    [Fact]
    public async Task RunAndPersistAsync_uses_desired_state_runner_when_requested()
    {
        var legacyCalled = false;
        var desiredCalled = false;
        var router = new SetupExecutionRouter(
            (_, _, _, _, _, _) =>
            {
                legacyCalled = true;
                return Task.FromResult(new SetupRunArtifacts(
                    SetupReport.Passed(Array.Empty<StepExecution>()),
                    StatePath: "state.json",
                    LogPath: "setup.log",
                    ReportPath: "setup-report.md",
                    Alias: "axel"));
            },
            (_, _, _, _, _, _) =>
            {
                desiredCalled = true;
                return Task.FromResult(new DesiredStateSetupRunArtifacts(
                    CreateDesiredStateReport(success: true),
                    StatePath: "state.json",
                    LogPath: "setup.log",
                    ReportPath: "setup-report.md",
                    Alias: "axel"));
            });

        var artifacts = await router.RunAndPersistAsync(
            new SetupOptions(SetupMode.Install, Engine: SetupExecutionEngine.DesiredState),
            workspaceRoot: "workspace",
            studentAlias: "axel",
            new NoopCommandRunner(),
            jsonWriter: null,
            CancellationToken.None);

        Assert.False(legacyCalled);
        Assert.True(desiredCalled);
        Assert.Equal(SetupExecutionEngine.DesiredState, artifacts.Engine);
        Assert.Equal("tu copia en GitHub", Assert.Single(artifacts.Blocks).Name);
    }

    private static DesiredStateSetupReport CreateDesiredStateReport(bool success)
    {
        var verify = success
            ? new SetupNodeResult("github-ready", "tu copia en GitHub", SetupNodeStatus.Ready, "Tu copia en GitHub ya esta lista.", "github-ready: ok", Array.Empty<StepExecution>())
            : new SetupNodeResult("github-ready", "tu copia en GitHub", SetupNodeStatus.Failed, "No pude completar tu copia en GitHub.", "github-ready: failed", Array.Empty<StepExecution>());

        return new DesiredStateSetupReport(
            success,
            new[]
            {
                new DesiredStateNodeReport(
                    "github-ready",
                    "tu copia en GitHub",
                    new SetupNodeResult("github-ready", "tu copia en GitHub", SetupNodeStatus.Ready, "Tu copia en GitHub ya esta lista.", "github-ready.detect: ok", Array.Empty<StepExecution>()),
                    new SetupNodePlan(
                        "github-ready",
                        "tu copia en GitHub",
                        SetupNodeStatus.Ready,
                        "Tu copia en GitHub ya esta lista.",
                        "github-ready.plan: ready",
                        RequiresChanges: false,
                        ApplyActions: Array.Empty<SetupPlannedAction>(),
                        RepairActions: Array.Empty<SetupRepairAction>()),
                    null,
                    null,
                    verify),
            });
    }

    private sealed class NoopCommandRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandResult.Success(string.Empty));
        }
    }
}