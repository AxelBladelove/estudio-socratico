using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class GitSafetyBackupStepTests
{
    [Fact]
    public async Task VerifyAsync_returns_ok_when_worktree_is_clean()
    {
        var runner = new QueueCommandRunner(CommandResult.Success(string.Empty));
        var step = new GitSafetyBackupStep(runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("git", "status --porcelain"), runner.Calls.Single());
    }

    [Fact]
    public async Task VerifyAsync_warns_when_worktree_has_changes()
    {
        var runner = new QueueCommandRunner(CommandResult.Success(" M package.json"));
        var step = new GitSafetyBackupStep(runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.IsWarning);
        Assert.Contains("cambios locales", result.Message);
    }

    [Fact]
    public async Task InstallAsync_commits_backup_when_worktree_is_dirty()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success(" M package.json"),
            CommandResult.Success(string.Empty),
            CommandResult.Success("[main abc123] backup"));
        var step = new GitSafetyBackupStep(runner);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("git", "status --porcelain"), runner.Calls[0]);
        Assert.Equal(("git", "add -A"), runner.Calls[1]);
        Assert.Equal(("git", "commit -m \"chore(estudio): backup automatico antes de actualizar\""), runner.Calls[2]);
    }

    [Fact]
    public async Task InstallAsync_treats_nothing_to_commit_as_ok_after_add()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("?? temp.txt"),
            CommandResult.Success(string.Empty),
            CommandResult.Failure(1, string.Empty, "nothing to commit, working tree clean"));
        var step = new GitSafetyBackupStep(runner);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
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

        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(_results.Dequeue());
        }
    }
}
