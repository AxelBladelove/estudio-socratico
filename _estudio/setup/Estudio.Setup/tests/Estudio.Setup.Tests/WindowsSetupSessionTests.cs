using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Windows;

namespace Estudio.Setup.Tests;

public sealed class WindowsSetupSessionTests
{
    [Fact]
    public async Task StartInstallationAsync_runs_desired_state_with_selected_folder()
    {
        SetupOptions? capturedOptions = null;
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify, StateRoot: "state-root"),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new NoopCommandRunner(),
            runDesiredStateAsync: (options, workspaceRoot, alias, commandRunner, progress, cancellationToken) =>
            {
                capturedOptions = options;
                return Task.FromResult(CreateArtifacts(success: true, workspaceRoot, alias));
            },
            userProfileProvider: () => TestPaths.UserProfile);
        session.UseInstallationFolder(TestPaths.WorkspaceRoot);

        await session.StartInstallationAsync(SetupMode.Install, CancellationToken.None);

        Assert.NotNull(capturedOptions);
        Assert.Equal(SetupExecutionEngine.DesiredState, capturedOptions!.Engine);
        Assert.Equal(TestPaths.WorkspaceRoot, capturedOptions.WorkspaceRoot);
        Assert.True(session.IsFinishedScreen);
    }

    [Fact]
    public async Task RetryFailedAsync_runs_repair_only_for_failed_blocks()
    {
        var calls = new List<SetupOptions>();
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new NoopCommandRunner(),
            runDesiredStateAsync: async (options, workspaceRoot, alias, commandRunner, progress, cancellationToken) =>
            {
                calls.Add(options);
                if (calls.Count == 1)
                {
                    await progress.ReportAsync(new DesiredStateNodePhaseFinished(
                        "compiler-ready",
                        "el compilador de C",
                        "verify",
                        new SetupNodeResult("compiler-ready", "el compilador de C", SetupNodeStatus.Failed, "No pude instalar MSYS2 automaticamente.", "msys2-toolchain failed", Array.Empty<StepExecution>())), cancellationToken);
                    return CreateArtifacts(success: false, workspaceRoot, alias);
                }

                return CreateArtifacts(success: true, workspaceRoot, alias);
            },
            userProfileProvider: () => TestPaths.UserProfile);
        session.UseInstallationFolder(TestPaths.WorkspaceRoot);

        await session.StartInstallationAsync(SetupMode.Install, CancellationToken.None);
        await session.RetryFailedAsync(CancellationToken.None);

        Assert.Equal(2, calls.Count);
        Assert.Equal(SetupMode.Repair, calls[1].Mode);
        Assert.Equal(new[] { "compiler-ready" }, calls[1].OnlyStepIds);
    }

    [Fact]
    public async Task InitializeAsync_refreshes_existing_github_and_exercism_state()
    {
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new ScriptedCommandRunner((fileName, arguments) => (fileName, arguments) switch
            {
                ("gh", "auth status") => CommandResult.Success("Logged in to github.com"),
                ("gh", "auth status --json hosts") => CommandResult.Success(
                    """
                    {"hosts":{"github.com":[{"state":"success","active":true,"host":"github.com","login":"AxelBladelove"}]}}
                    """),
                ("gh", "api user") => CommandResult.Success(
                    """
                    {"login":"AxelBladelove"}
                    """),
                ("exercism", "configure --show") => CommandResult.Success("Token: already-configured"),
                ("exercism", "download --track c --exercise hello-world") => CommandResult.Success("downloaded"),
                _ => CommandResult.NotFound(fileName),
            }),
            userProfileProvider: () => TestPaths.UserProfile);

        await session.InitializeAsync(CancellationToken.None);

        Assert.True(session.IsGitHubConnected);
        Assert.Equal("GitHub conectado", session.GitHubCardTitle);
        Assert.Contains("@AxelBladelove", session.GitHubCardSubtitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Confirma esta cuenta", session.GitHubStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ya están listos", session.ExercismStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateExercismTokenAsync_opens_track_page_when_c_track_must_be_joined()
    {
        var uriLauncher = new RecordingUriLauncher();
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new ScriptedCommandRunner((fileName, arguments) => (fileName, arguments) switch
            {
                ("exercism", "configure --show") => CommandResult.Success("There is no token configured"),
                ("exercism", var command) when command.StartsWith("configure --token", StringComparison.Ordinal) => CommandResult.Success("configured"),
                ("exercism", "prepare") => CommandResult.Success("prepared"),
                ("exercism", "download --track c --exercise hello-world") => CommandResult.Failure(1, string.Empty, "track_not_joined"),
                ("gh", "auth status") => CommandResult.NotFound("gh"),
                _ => CommandResult.NotFound(fileName),
            }),
            uriLauncher: uriLauncher,
            userProfileProvider: () => TestPaths.UserProfile);

        await session.UpdateExercismTokenAsync("token-123", CancellationToken.None);

        Assert.Equal("Activar ejercicios de C", session.ExercismCardTitle);
        Assert.Contains("Voy a abrir Exercism", session.ExercismStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Steps.ExercismCTrackStep.CTrackUrl, uriLauncher.LastUrl);
    }

    [Fact]
    public void GetGuidedSolution_returns_human_guide_for_known_block()
    {
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new NoopCommandRunner(),
            userProfileProvider: () => TestPaths.UserProfile);

        var solution = session.GetGuidedSolution("compiler-ready");

        Assert.NotNull(solution);
        Assert.Equal("compiler-ready", solution!.BlockId);
        Assert.NotEmpty(solution.Steps);
    }

    [Fact]
    public async Task GetGitHubAccountSwitchContextAsync_detects_multiple_known_accounts()
    {
        var runner = new ScriptedCommandRunner((fileName, arguments) => (fileName, arguments) switch
        {
            ("gh", "auth status --json hosts") => CommandResult.Success(
                """
                {"hosts":{"github.com":[{"active":true,"login":"AxelBladelove"},{"active":false,"login":"OtraCuenta"}]}}
                """),
            _ => CommandResult.NotFound(fileName),
        });
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => runner,
            userProfileProvider: () => TestPaths.UserProfile);

        var context = await session.GetGitHubAccountSwitchContextAsync(CancellationToken.None);

        Assert.Equal(2, context.KnownAccountCount);
        Assert.True(context.HasMultipleAccounts);
        Assert.True(context.CanSwitchAutomatically);
        Assert.Equal("AxelBladelove", context.ActiveUserName);
    }

    [Fact]
    public async Task SwitchGitHubAccountAutomaticallyAsync_revalidates_active_user_with_gh_api_user()
    {
        var runner = new ScriptedCommandRunner((fileName, arguments) => (fileName, arguments) switch
        {
            ("gh", "auth switch --hostname github.com") => CommandResult.Success("switched"),
            ("gh", "auth status") => CommandResult.Success("Logged in to github.com"),
            ("gh", "auth status --json hosts") => CommandResult.Success(
                """
                {"hosts":{"github.com":[{"active":true,"login":"CuentaNueva"},{"active":false,"login":"AxelBladelove"}]}}
                """),
            ("gh", "api user") => CommandResult.Success(
                """
                {"login":"CuentaNueva"}
                """),
            _ => CommandResult.NotFound(fileName),
        });
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => runner,
            userProfileProvider: () => TestPaths.UserProfile);

        var switched = await session.SwitchGitHubAccountAutomaticallyAsync(CancellationToken.None);

        Assert.True(switched);
        Assert.Contains("@CuentaNueva", session.GitHubCardSubtitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(runner.Calls, call => call == ("gh", "api user"));
    }

    [Fact]
    public async Task ConnectGitHubAsync_reports_human_error_when_terminal_cannot_be_opened()
    {
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new NoopCommandRunner(),
            interactiveCommandLauncher: _ => "launcher failed",
            userProfileProvider: () => TestPaths.UserProfile);

        await session.ConnectGitHubAsync(CancellationToken.None);

        Assert.True(session.HasGitHubActionError);
        Assert.Equal("No pude abrir GitHub automáticamente.", session.GitHubActionErrorHeadline);
        Assert.Contains("intentar de nuevo", session.GitHubActionErrorBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartGitHubReLoginInTerminal_uses_valid_logout_command_without_yes_flag()
    {
        string? launchedCommand = null;
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new ScriptedCommandRunner((fileName, arguments) => (fileName, arguments) switch
            {
                ("gh", "auth status") => CommandResult.Success("Logged in to github.com"),
                ("gh", "auth status --json hosts") => CommandResult.Success(
                    """
                    {"hosts":{"github.com":[{"active":true,"login":"AxelBladelove"}]}}
                    """),
                ("gh", "api user") => CommandResult.Success(
                    """
                    {"login":"AxelBladelove"}
                    """),
                ("exercism", "configure --show") => CommandResult.Success("There is no token configured"),
                _ => CommandResult.NotFound(fileName),
            }),
            interactiveCommandLauncher: command =>
            {
                launchedCommand = command;
                return null;
            },
            userProfileProvider: () => TestPaths.UserProfile);

        await session.InitializeAsync(CancellationToken.None);

        var started = session.StartGitHubReLoginInTerminal();

        Assert.True(started);
        Assert.NotNull(launchedCommand);
        Assert.Contains("gh auth logout --hostname github.com --user AxelBladelove", launchedCommand, StringComparison.Ordinal);
        Assert.Contains("gh auth login --web --hostname github.com --git-protocol https", launchedCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("--yes", launchedCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickReview_folder_message_is_only_present_once_in_checks()
    {
        var session = new WindowsSetupSession(
            baselineOptions: new SetupOptions(SetupMode.Verify),
            currentDirectoryProvider: () => TestPaths.CurrentDirectory,
            commandRunnerFactory: () => new NoopCommandRunner(),
            userProfileProvider: () => TestPaths.UserProfile);

        var folderMessages = session.QuickChecks.Count(check => check.Message.Contains("Tu carpeta de estudio será", StringComparison.Ordinal));

        Assert.Equal(1, folderMessages);
        Assert.DoesNotContain(session.QuickChecks, check => string.Equals(check.Title, check.Message, StringComparison.Ordinal));
    }

    private static DesiredStateSetupRunArtifacts CreateArtifacts(bool success, string workspaceRoot, string alias)
    {
        var verify = success
            ? new SetupNodeResult("compiler-ready", "el compilador de C", SetupNodeStatus.Ready, "El compilador de C ya esta listo.", "compiler-ready: ok", Array.Empty<StepExecution>())
            : new SetupNodeResult("compiler-ready", "el compilador de C", SetupNodeStatus.Failed, "No pude instalar MSYS2 automaticamente.", "msys2-toolchain failed", Array.Empty<StepExecution>());

        var report = new DesiredStateSetupReport(
            success,
            new[]
            {
                new DesiredStateNodeReport(
                    "compiler-ready",
                    "el compilador de C",
                    verify,
                    new SetupNodePlan("compiler-ready", "el compilador de C", verify.Status, verify.HumanMessage, verify.TechnicalMessage, RequiresChanges: !success, ApplyActions: Array.Empty<SetupPlannedAction>(), RepairActions: Array.Empty<SetupRepairAction>()),
                    success ? null : "repair",
                    success ? null : verify,
                    verify),
            });

        return new DesiredStateSetupRunArtifacts(report, "state.json", "setup.log", "setup-report.md", alias);
    }

    private sealed class NoopCommandRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandResult.Success(string.Empty));
        }
    }

    private sealed class ScriptedCommandRunner : ICommandRunner
    {
        private readonly Func<string, string, CommandResult> _handler;
        public List<(string FileName, string Arguments)> Calls { get; } = new();

        public ScriptedCommandRunner(Func<string, string, CommandResult> handler)
        {
            _handler = handler;
        }

        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            Calls.Add((fileName, arguments));
            return Task.FromResult(_handler(fileName, arguments));
        }
    }

    private sealed class RecordingUriLauncher : IUriLauncher
    {
        public string? LastUrl { get; private set; }

        public void Open(string url)
        {
            LastUrl = url;
        }
    }

    private static class TestPaths
    {
        public static string CurrentDirectory => Path.Combine(Path.GetTempPath(), "estudio-ui-current");
        public static string WorkspaceRoot => Path.Combine(Path.GetTempPath(), "estudio-ui-workspace");
        public static string UserProfile => Path.GetTempPath();
    }
}