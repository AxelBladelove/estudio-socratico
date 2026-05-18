using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public sealed class SetupRunCoordinatorTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-setup-coordinator-{Guid.NewGuid():N}");

    [Fact]
    public async Task RunAndPersistAsync_runs_steps_persists_artifacts_and_reports_progress()
    {
        var progress = new RecordingProgressSink();
        var options = new SetupOptions(SetupMode.Verify, StateRoot: _tempRoot);
        var workspaceRoot = Path.Combine(_tempRoot, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        var coordinator = new SetupRunCoordinator(
            (_, _, _) => new ISetupStep[] { new PassingStep("git") },
            (_, _) => Task.FromResult<string?>("octocat"));

        var artifacts = await coordinator.RunAndPersistAsync(
            options,
            workspaceRoot,
            "axel",
            new NoopCommandRunner(),
            progress,
            CancellationToken.None);

        Assert.True(artifacts.Report.Success);
        Assert.Equal(Path.Combine(_tempRoot, "setup-state.json"), artifacts.StatePath);
        Assert.Equal(Path.Combine(_tempRoot, "setup-report.md"), artifacts.ReportPath);
        Assert.True(File.Exists(artifacts.StatePath));
        Assert.True(File.Exists(artifacts.LogPath));
        Assert.True(File.Exists(artifacts.ReportPath));
        Assert.Contains("\"alias\": \"axel\"", File.ReadAllText(artifacts.StatePath));
        Assert.Contains("\"githubUser\": \"octocat\"", File.ReadAllText(artifacts.StatePath));
        Assert.Equal(new[] { "run-started:Verify", "phase-started:git.detect", "phase-finished:git.detect", "phase-started:git.verify", "phase-finished:git.verify", "run-finished:ok" }, progress.Events);
    }

    [Fact]
    public async Task RunAndPersistAsync_records_github_user_after_forced_relogin_flow()
    {
        var options = new SetupOptions(
            SetupMode.Update,
            StateRoot: _tempRoot,
            ForceGitHubRelogin: true);
        var workspaceRoot = Path.Combine(_tempRoot, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        var coordinator = new SetupRunCoordinator(
            (_, _, _) => new ISetupStep[] { new PassingStep("github-auth") },
            (_, _) => Task.FromResult<string?>("new-octocat"));

        var artifacts = await coordinator.RunAndPersistAsync(
            options,
            workspaceRoot,
            "axel",
            new NoopCommandRunner(),
            NullSetupProgressSink.Instance,
            CancellationToken.None);

        Assert.True(artifacts.Report.Success);
        Assert.Contains("\"githubUser\": \"new-octocat\"", File.ReadAllText(artifacts.StatePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private sealed class PassingStep : ISetupStep
    {
        public PassingStep(string id)
        {
            Id = id;
            Name = id;
        }

        public string Id { get; }
        public string Name { get; }

        public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(StepResult.Ok("detected"));
        }

        public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(StepResult.Ok("installed"));
        }

        public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(StepResult.Ok("updated"));
        }

        public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(StepResult.Ok("repaired"));
        }

        public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(StepResult.Ok("verified"));
        }
    }

    private sealed class RecordingProgressSink : ISetupProgressSink
    {
        private readonly List<string> _events = new();

        public IReadOnlyList<string> Events => _events;

        public Task ReportAsync(SetupProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            switch (progressEvent)
            {
                case SetupRunStarted started:
                    _events.Add($"run-started:{started.Mode}");
                    break;
                case SetupPhaseStarted started:
                    _events.Add($"phase-started:{started.StepId}.{started.Phase}");
                    break;
                case SetupPhaseFinished finished:
                    _events.Add($"phase-finished:{finished.Execution.StepId}.{finished.Execution.Phase}");
                    break;
                case SetupRunFinished finished:
                    _events.Add($"run-finished:{(finished.Report.Success ? "ok" : "failed")}");
                    break;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class NoopCommandRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandResult.Success(string.Empty));
        }
    }
}
