using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public sealed class GitIdentityStepTests
{
    [Fact]
    public async Task UpdateAsync_writes_local_git_identity_from_github_user_and_alias()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("octocat"),
            CommandResult.Success(string.Empty),
            CommandResult.Success(string.Empty),
            CommandResult.Success(string.Empty));
        var step = new GitIdentityStep(runner, "axel");

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "api user --jq .login"), runner.Calls[0]);
        Assert.Equal(("git", "config --local github.user octocat"), runner.Calls[1]);
        Assert.Equal(("git", "config --local user.name axel"), runner.Calls[2]);
        Assert.Equal(("git", "config --local user.email octocat@users.noreply.github.com"), runner.Calls[3]);
    }

    [Fact]
    public async Task VerifyAsync_succeeds_when_local_git_identity_matches()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("octocat"),
            CommandResult.Success("octocat"),
            CommandResult.Success("axel"),
            CommandResult.Success("octocat@users.noreply.github.com"));
        var step = new GitIdentityStep(runner, "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("git", "config --local --get github.user"), runner.Calls[1]);
        Assert.Equal(("git", "config --local --get user.name"), runner.Calls[2]);
        Assert.Equal(("git", "config --local --get user.email"), runner.Calls[3]);
    }

    [Fact]
    public async Task VerifyAsync_reports_missing_when_alias_is_not_configured_as_user_name()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("octocat"),
            CommandResult.Success("octocat"),
            CommandResult.Success("old"),
            CommandResult.Success("octocat@users.noreply.github.com"));
        var step = new GitIdentityStep(runner, "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("user.name", result.Message);
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
