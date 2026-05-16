using Estudio.Setup.Core;

namespace Estudio.Setup.Tests;

public class SetupEngineTests
{
    [Fact]
    public async Task Verify_mode_runs_detect_and_verify_without_installing()
    {
        var step = new RecordingStep(
            id: "git",
            detect: StepResult.Ok("git found"),
            verify: StepResult.Ok("git --version ok"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Verify), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "git.detect", "git.verify" }, step.Calls);
        Assert.Equal("verify-final", report.LastSuccessfulStep);
    }

    [Fact]
    public async Task Install_mode_installs_missing_step_before_verification()
    {
        var step = new RecordingStep(
            id: "powershell7",
            detect: StepResult.Missing("pwsh missing"),
            install: StepResult.Ok("pwsh installed"),
            verify: StepResult.Ok("pwsh --version ok"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "powershell7.detect", "powershell7.install", "powershell7.verify" }, step.Calls);
        Assert.Equal("verify-final", report.LastSuccessfulStep);
    }

    [Fact]
    public async Task Install_mode_runs_action_for_warning_detection_before_verification()
    {
        var step = new RecordingStep(
            id: "git-safety-backup",
            detect: StepResult.Warning("dirty worktree"),
            install: StepResult.Ok("backup committed"),
            verify: StepResult.Ok("clean"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "git-safety-backup.detect", "git-safety-backup.install", "git-safety-backup.verify" }, step.Calls);
    }

    [Fact]
    public async Task Update_mode_updates_detected_step_before_verification()
    {
        var step = new RecordingStep(
            id: "node",
            detect: StepResult.Ok("node found"),
            install: StepResult.Ok("node updated"),
            verify: StepResult.Ok("node ok"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Update), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "node.detect", "node.update", "node.verify" }, step.Calls);
    }

    [Fact]
    public async Task Repair_mode_repairs_failed_detection_before_verification()
    {
        var step = new RecordingStep(
            id: "vscode-settings",
            detect: StepResult.Fail("settings invalid"),
            install: StepResult.Ok("settings repaired"),
            verify: StepResult.Ok("settings ok"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Repair), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "vscode-settings.detect", "vscode-settings.repair", "vscode-settings.verify" }, step.Calls);
    }

    [Fact]
    public async Task Repair_mode_runs_action_for_warning_detection_before_verification()
    {
        var step = new RecordingStep(
            id: "git-safety-backup",
            detect: StepResult.Warning("dirty worktree"),
            install: StepResult.Ok("backup committed"),
            verify: StepResult.Ok("clean"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Repair), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "git-safety-backup.detect", "git-safety-backup.repair", "git-safety-backup.verify" }, step.Calls);
    }

    [Fact]
    public async Task Reinstall_mode_repairs_detected_step_before_verification()
    {
        var step = new RecordingStep(
            id: "vscode-extension",
            detect: StepResult.Ok("extension found"),
            install: StepResult.Ok("extension reinstalled"),
            verify: StepResult.Ok("extension ok"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Reinstall), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "vscode-extension.detect", "vscode-extension.repair", "vscode-extension.verify" }, step.Calls);
        Assert.Contains(report.Steps, step => step.StepId == "vscode-extension" && step.Phase == "reinstall");
    }

    [Fact]
    public async Task Uninstall_mode_runs_uninstall_action_without_final_verification()
    {
        var step = new RecordingUninstallStep(
            id: "vscode-extension",
            detect: StepResult.Ok("extension found"),
            uninstall: StepResult.Ok("extension removed"),
            verify: StepResult.Fail("should not verify after uninstall"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Uninstall), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "vscode-extension.detect", "vscode-extension.uninstall" }, step.Calls);
        Assert.DoesNotContain(report.Steps, step => step.Phase == "verify");
    }

    [Fact]
    public async Task Uninstall_mode_treats_steps_without_uninstall_as_idempotent_noop()
    {
        var step = new RecordingStep(
            id: "git",
            detect: StepResult.Ok("git found"),
            verify: StepResult.Fail("should not verify after uninstall"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Uninstall), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(new[] { "git.detect" }, step.Calls);
        var uninstall = Assert.Single(report.Steps, step => step.Phase == "uninstall");
        Assert.True(uninstall.Result.Success);
        Assert.True(uninstall.Result.IsWarning);
        Assert.Contains("no requiere desinstalacion", uninstall.Result.Message);
    }


    [Fact]
    public async Task Verify_mode_reports_missing_step_without_running_verify()
    {
        var step = new RecordingStep(
            id: "gh",
            detect: StepResult.Missing("gh missing"),
            verify: StepResult.Ok("should not run"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Verify), CancellationToken.None);

        Assert.False(report.Success);
        Assert.Equal(new[] { "gh.detect" }, step.Calls);
        Assert.Equal("start", report.LastSuccessfulStep);
        Assert.Equal("gh missing", report.Steps.Single().Result.Message);
    }

    [Fact]
    public async Task Verify_mode_continues_after_missing_step_to_report_all_prerequisites()
    {
        var first = new RecordingStep(
            id: "git",
            detect: StepResult.Ok("git found"),
            verify: StepResult.Ok("git ok"));
        var missing = new RecordingStep(
            id: "vscode",
            detect: StepResult.Missing("code missing"),
            verify: StepResult.Ok("should not run"));
        var last = new RecordingStep(
            id: "gcc",
            detect: StepResult.Ok("gcc found"),
            verify: StepResult.Ok("gcc ok"));
        var engine = new SetupEngine(new[] { first, missing, last });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Verify), CancellationToken.None);

        Assert.False(report.Success);
        Assert.Equal(new[] { "git.detect", "git.verify" }, first.Calls);
        Assert.Equal(new[] { "vscode.detect" }, missing.Calls);
        Assert.Equal(new[] { "gcc.detect", "gcc.verify" }, last.Calls);
        Assert.Equal(new[] { "git", "git", "vscode", "gcc", "gcc" }, report.Steps.Select(step => step.StepId));
    }

    [Fact]
    public async Task Verify_mode_keeps_last_successful_step_before_first_critical_failure()
    {
        var first = new RecordingStep(
            id: "git",
            detect: StepResult.Ok("git found"),
            verify: StepResult.Ok("git ok"));
        var missing = new RecordingStep(
            id: "vscode",
            detect: StepResult.Missing("code missing"));
        var later = new RecordingStep(
            id: "node",
            detect: StepResult.Ok("node found"),
            verify: StepResult.Ok("node ok"));
        var engine = new SetupEngine(new[] { first, missing, later });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Verify), CancellationToken.None);

        Assert.False(report.Success);
        Assert.Equal("git", report.LastSuccessfulStep);
    }

    [Fact]
    public async Task Install_mode_records_unexpected_step_exception_as_failed_execution()
    {
        var step = new ThrowingStep("runtime-config", new InvalidOperationException("download failed"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.False(report.Success);
        var execution = Assert.Single(report.Steps);
        Assert.Equal("runtime-config", execution.StepId);
        Assert.Equal("detect", execution.Phase);
        Assert.False(execution.Result.Success);
        Assert.Contains("download failed", execution.Result.Message);
    }

    [Fact]
    public async Task Verify_mode_continues_after_unexpected_step_exception()
    {
        var first = new ThrowingStep("runtime-config", new InvalidOperationException("download failed"));
        var second = new RecordingStep(
            id: "git",
            detect: StepResult.Ok("git found"),
            verify: StepResult.Ok("git ok"));
        var engine = new SetupEngine(new ISetupStep[] { first, second });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Verify), CancellationToken.None);

        Assert.False(report.Success);
        Assert.Equal(new[] { "runtime-config", "git", "git" }, report.Steps.Select(step => step.StepId));
        Assert.Equal(new[] { "git.detect", "git.verify" }, second.Calls);
    }

    [Fact]
    public async Task Repair_mode_runs_only_selected_steps_when_filter_is_present()
    {
        var skipped = new RecordingStep(
            id: "git-safety-backup",
            detect: StepResult.Warning("dirty worktree"),
            install: StepResult.Ok("backup committed"),
            verify: StepResult.Ok("clean"));
        var selected = new RecordingStep(
            id: "vscode-settings",
            detect: StepResult.Missing("settings missing"),
            install: StepResult.Ok("settings repaired"),
            verify: StepResult.Ok("settings ok"));
        var engine = new SetupEngine(new[] { skipped, selected });

        var report = await engine.RunAsync(
            new SetupOptions(SetupMode.Repair, OnlyStepIds: new[] { "vscode-settings" }),
            CancellationToken.None);

        Assert.True(report.Success);
        Assert.Empty(skipped.Calls);
        Assert.Equal(new[] { "vscode-settings.detect", "vscode-settings.repair", "vscode-settings.verify" }, selected.Calls);
    }

    [Fact]
    public async Task RunAsync_emits_progress_events_for_cli_and_tui_consumers()
    {
        var progress = new RecordingProgressSink();
        var step = new RecordingStep(
            id: "git",
            detect: StepResult.Ok("git found"),
            verify: StepResult.Ok("git ok"));
        var engine = new SetupEngine(new[] { step }, progress);

        await engine.RunAsync(new SetupOptions(SetupMode.Verify), CancellationToken.None);

        Assert.Equal(
            new[]
            {
                "run-started:Verify",
                "phase-started:git.detect",
                "phase-finished:git.detect:ok",
                "phase-started:git.verify",
                "phase-finished:git.verify:ok",
                "run-finished:ok",
            },
            progress.Events);
    }

    [Fact]
    public async Task RunAsync_emits_run_finished_event_when_a_step_fails()
    {
        var progress = new RecordingProgressSink();
        var step = new RecordingStep(
            id: "git",
            detect: StepResult.Fail("git broken"));
        var engine = new SetupEngine(new[] { step }, progress);

        await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.Contains("phase-finished:git.detect:failed", progress.Events);
        Assert.Equal("run-finished:failed", progress.Events.Last());
    }

    private sealed class RecordingStep : ISetupStep
    {
        private readonly StepResult _detect;
        private readonly StepResult _install;
        private readonly StepResult _verify;
        private readonly List<string> _calls = new();

        public RecordingStep(
            string id,
            StepResult? detect = null,
            StepResult? install = null,
            StepResult? verify = null)
        {
            Id = id;
            Name = id;
            _detect = detect ?? StepResult.Ok("detected");
            _install = install ?? StepResult.Ok("installed");
            _verify = verify ?? StepResult.Ok("verified");
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Calls => _calls;

        public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.detect");
            return Task.FromResult(_detect);
        }

        public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.install");
            return Task.FromResult(_install);
        }

        public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.update");
            return Task.FromResult(_install);
        }

        public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.repair");
            return Task.FromResult(_install);
        }

        public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.verify");
            return Task.FromResult(_verify);
        }
    }

    private sealed class RecordingUninstallStep : ISetupStep, IUninstallSetupStep
    {
        private readonly StepResult _detect;
        private readonly StepResult _uninstall;
        private readonly StepResult _verify;
        private readonly List<string> _calls = new();

        public RecordingUninstallStep(
            string id,
            StepResult? detect = null,
            StepResult? uninstall = null,
            StepResult? verify = null)
        {
            Id = id;
            Name = id;
            _detect = detect ?? StepResult.Ok("detected");
            _uninstall = uninstall ?? StepResult.Ok("uninstalled");
            _verify = verify ?? StepResult.Ok("verified");
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Calls => _calls;

        public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.detect");
            return Task.FromResult(_detect);
        }

        public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.install");
            return Task.FromResult(StepResult.Ok("installed"));
        }

        public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.update");
            return Task.FromResult(StepResult.Ok("updated"));
        }

        public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.repair");
            return Task.FromResult(StepResult.Ok("repaired"));
        }

        public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.verify");
            return Task.FromResult(_verify);
        }

        public Task<StepResult> UninstallAsync(SetupContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"{Id}.uninstall");
            return Task.FromResult(_uninstall);
        }
    }

    private sealed class ThrowingStep : ISetupStep
    {
        private readonly Exception _exception;

        public ThrowingStep(string id, Exception exception)
        {
            Id = id;
            Name = id;
            _exception = exception;
        }

        public string Id { get; }
        public string Name { get; }

        public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
        {
            throw _exception;
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
                    _events.Add($"phase-finished:{finished.Execution.StepId}.{finished.Execution.Phase}:{StatusFor(finished.Execution.Result)}");
                    break;
                case SetupRunFinished finished:
                    _events.Add($"run-finished:{(finished.Report.Success ? "ok" : "failed")}");
                    break;
            }

            return Task.CompletedTask;
        }

        private static string StatusFor(StepResult result)
        {
            if (result.IsWarning)
            {
                return "warning";
            }

            if (result.Success)
            {
                return "ok";
            }

            return result.IsMissing ? "missing" : "failed";
        }
    }
}
