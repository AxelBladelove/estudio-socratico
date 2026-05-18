using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class GitHubAuthStepTests
{
    [Fact]
    public async Task VerifyAsync_succeeds_when_gh_auth_status_succeeds()
    {
        var runner = new QueueCommandRunner(CommandResult.Success("Logged in to github.com"));
        var step = new GitHubAuthStep(runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "auth status"), runner.Calls.Single());
    }

    [Fact]
    public async Task VerifyAsync_returns_missing_when_gh_is_not_available()
    {
        var runner = new QueueCommandRunner(CommandResult.NotFound("gh"));
        var step = new GitHubAuthStep(runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("gh", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_returns_missing_when_user_is_not_authenticated()
    {
        var runner = new QueueCommandRunner(CommandResult.Failure(1, string.Empty, "You are not logged into any GitHub hosts"));
        var step = new GitHubAuthStep(runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("gh auth login", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_reuses_existing_session_when_relogin_was_not_requested()
    {
        var runner = new QueueCommandRunner(CommandResult.Success("Logged in to github.com"));
        var step = new GitHubAuthStep(runner);

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "auth status"), runner.Calls.Single());
    }

    [Fact]
    public async Task RepairAsync_reuses_existing_session_when_relogin_was_not_requested()
    {
        var runner = new QueueCommandRunner(CommandResult.Success("Logged in to github.com"));
        var step = new GitHubAuthStep(runner);

        var result = await step.RepairAsync(new SetupContext(new SetupOptions(SetupMode.Reinstall)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "auth status"), runner.Calls.Single());
    }

    [Fact]
    public async Task UpdateAsync_logs_out_and_runs_web_login_when_relogin_is_requested()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("logged out"),
            CommandResult.Success("logged in"),
            CommandResult.Success("Logged in to github.com"));
        var step = new GitHubAuthStep(runner);

        var result = await step.UpdateAsync(
            new SetupContext(new SetupOptions(SetupMode.Update, ForceGitHubRelogin: true)),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "auth logout --hostname github.com"), runner.Calls[0]);
        Assert.Equal(("gh", "auth login --hostname github.com --web --git-protocol https"), runner.Calls[1]);
        Assert.Equal(("gh", "auth status"), runner.Calls[2]);
    }

    [Fact]
    public async Task UpdateAsync_continues_login_when_forced_logout_reports_no_existing_session()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Failure(1, string.Empty, "not logged in"),
            CommandResult.Success("logged in"),
            CommandResult.Success("Logged in to github.com"));
        var step = new GitHubAuthStep(runner);

        var result = await step.UpdateAsync(
            new SetupContext(new SetupOptions(SetupMode.Update, ForceGitHubRelogin: true)),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "auth logout --hostname github.com"), runner.Calls[0]);
        Assert.Equal(("gh", "auth login --hostname github.com --web --git-protocol https"), runner.Calls[1]);
        Assert.Equal(("gh", "auth status"), runner.Calls[2]);
    }

    [Fact]
    public async Task InstallAsync_runs_guided_web_login_when_missing()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("logged in"),
            CommandResult.Success("Logged in to github.com"));
        var step = new GitHubAuthStep(runner);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "auth login --hostname github.com --web --git-protocol https"), runner.Calls[0]);
        Assert.Equal(("gh", "auth status"), runner.Calls[1]);
    }

    private sealed class QueueCommandRunner : ICommandRunner
    {
        private readonly Queue<CommandResult> _results;
        private readonly List<(string FileName, string Arguments)> _calls = new();

        public QueueCommandRunner(params CommandResult[] results)
        {
            _results = new Queue<CommandResult>(results);
        }

        public IReadOnlyList<(string FileName, string Arguments)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(_results.Dequeue());
        }
    }
}
