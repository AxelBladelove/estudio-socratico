using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class GitRemoteStepTests
{
    [Fact]
    public async Task VerifyAsync_succeeds_when_origin_and_upstream_match_expected_urls()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Success("https://github.com/AxelBladelove/estudio-socratico-axel.git"),
            CommandResult.Success(GitRemoteStep.MainRepoUrl));
        var step = new GitRemoteStep(runner, alias: "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task VerifyAsync_accepts_ssh_remotes_that_match_expected_github_repositories()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Success("git@github.com:AxelBladelove/estudio-socratico-axel.git"),
            CommandResult.Success("git@github.com:AxelBladelove/estudio-socratico.git"));
        var step = new GitRemoteStep(runner, alias: "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task VerifyAsync_accepts_github_urls_without_git_suffix()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Success("https://github.com/AxelBladelove/estudio-socratico-axel"),
            CommandResult.Success("https://github.com/AxelBladelove/estudio-socratico"));
        var step = new GitRemoteStep(runner, alias: "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task VerifyAsync_reports_missing_when_upstream_is_missing()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Success("https://github.com/AxelBladelove/estudio-socratico-axel.git"),
            CommandResult.Failure(2, string.Empty, "No such remote"));
        var step = new GitRemoteStep(runner, alias: "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("upstream", result.Message);
    }

    [Fact]
    public async Task RepairAsync_sets_origin_and_adds_missing_upstream()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("AxelBladelove"),
            CommandResult.Success("https://github.com/AxelBladelove/estudio-socratico.git"),
            CommandResult.Success(string.Empty),
            CommandResult.Failure(2, string.Empty, "No such remote"),
            CommandResult.Success(string.Empty));
        var step = new GitRemoteStep(runner, alias: "axel");

        var result = await step.RepairAsync(new SetupContext(new SetupOptions(SetupMode.Repair)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("git", "remote set-url origin https://github.com/AxelBladelove/estudio-socratico-axel.git"), runner.Calls[2]);
        Assert.Equal(("git", $"remote add upstream {GitRemoteStep.MainRepoUrl}"), runner.Calls[4]);
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
