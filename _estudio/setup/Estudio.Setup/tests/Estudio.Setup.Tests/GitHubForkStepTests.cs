using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class GitHubForkStepTests
{
    [Fact]
    public async Task VerifyAsync_succeeds_when_expected_fork_exists()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Success("estudio-socratico-axel"));
        var step = new GitHubForkStep(runner, alias: "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "api user --jq .login"), runner.Calls[0]);
        Assert.Equal(("gh", "repo view AxelBladelove/estudio-socratico-axel --json name --jq .name"), runner.Calls[1]);
    }

    [Fact]
    public async Task InstallAsync_creates_expected_fork_when_missing()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Failure(1, string.Empty, "not found"),
            CommandResult.Success("forked"));
        var step = new GitHubForkStep(runner, alias: "axel");

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("gh", runner.Calls[2].FileName);
        Assert.Contains("repo fork AxelBladelove/estudio-socratico", runner.Calls[2].Arguments);
        Assert.Contains("--fork-name estudio-socratico-axel", runner.Calls[2].Arguments);
        Assert.DoesNotContain("--remote=false", runner.Calls[2].Arguments);
    }

    [Fact]
    public async Task InstallAsync_creates_work_repo_when_main_owner_cannot_fork_own_repo()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Failure(1, string.Empty, "not found"),
            CommandResult.Failure(1, string.Empty, "A single user account cannot own both a parent and fork."),
            CommandResult.Success("created"));
        var step = new GitHubForkStep(runner, alias: "axel");

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("gh", runner.Calls[3].FileName);
        Assert.Contains("repo create AxelBladelove/estudio-socratico-axel", runner.Calls[3].Arguments);
        Assert.Contains("--public", runner.Calls[3].Arguments);
    }

    [Fact]
    public async Task VerifyAsync_returns_missing_when_github_user_cannot_be_resolved()
    {
        var runner = new QueueCommandRunner(CommandResult.Failure(1, string.Empty, "not logged in"));
        var step = new GitHubForkStep(runner, alias: "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("GitHub", result.Message);
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
