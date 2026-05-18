using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class GitProjectUpdateStepTests
{
    [Fact]
    public async Task UpdateAsync_fetches_upstream_merges_main_and_pushes_origin()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("fetched"),
            CommandResult.Success("merged"),
            CommandResult.Success("pushed"));
        var step = new GitProjectUpdateStep(runner);

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("git", "fetch upstream"), runner.Calls[0]);
        Assert.Equal(("git", "merge upstream/main"), runner.Calls[1]);
        Assert.Equal(("git", "push origin main"), runner.Calls[2]);
    }

    [Fact]
    public async Task UpdateAsync_reports_failure_when_merge_conflicts()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("fetched"),
            CommandResult.Failure(1, string.Empty, "CONFLICT"));
        var step = new GitProjectUpdateStep(runner);

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("merge", result.Message);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task UpdateAsync_returns_warning_when_origin_is_archived_after_local_merge()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("fetched"),
            CommandResult.Success("merged"),
            CommandResult.Failure(1, string.Empty, "remote: This repository was archived so it is read-only."));
        var step = new GitProjectUpdateStep(runner);

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.IsWarning);
        Assert.Contains("solo lectura", result.Message);
        Assert.Equal(3, runner.Calls.Count);
    }

    [Fact]
    public async Task VerifyAsync_checks_that_upstream_and_origin_are_reachable()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("abc123"),
            CommandResult.Success("def456"));
        var step = new GitProjectUpdateStep(runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("git", "ls-remote upstream main"), runner.Calls[0]);
        Assert.Equal(("git", "ls-remote origin main"), runner.Calls[1]);
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
