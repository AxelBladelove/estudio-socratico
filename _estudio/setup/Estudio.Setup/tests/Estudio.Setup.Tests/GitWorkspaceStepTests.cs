using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public sealed class GitWorkspaceStepTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-git-workspace-{Guid.NewGuid():N}");

    [Fact]
    public async Task InstallAsync_clones_student_fork_into_clean_workspace()
    {
        var workspaceRoot = Path.Combine(_tempRoot, "workspace");
        var runner = new QueueCommandRunner(
            CommandResult.Success("octocat"),
            CommandResult.Success("cloned"));
        var step = new GitWorkspaceStep(runner, "axel", workspaceRoot);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "api user --jq .login", null), runner.Calls[0]);
        Assert.Equal(("git", $"clone \"https://github.com/octocat/estudio-socratico-axel.git\" \"{workspaceRoot}\"", null), runner.Calls[1]);
    }

    [Fact]
    public async Task InstallAsync_reuses_existing_git_workspace_without_cloning_again()
    {
        var workspaceRoot = Path.Combine(_tempRoot, "existing");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".git"));
        var runner = new QueueCommandRunner();
        var step = new GitWorkspaceStep(runner, "axel", workspaceRoot);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task InstallAsync_fails_when_target_folder_is_non_empty_and_not_a_repo()
    {
        var workspaceRoot = Path.Combine(_tempRoot, "dirty-target");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "README.txt"), "occupied");
        var step = new GitWorkspaceStep(new QueueCommandRunner(), "axel", workspaceRoot);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no esta vacia", result.Message);
    }

    private sealed class QueueCommandRunner : ICommandRunner
    {
        private readonly Queue<CommandResult> _results;
        private readonly List<(string FileName, string Arguments, string? WorkingDirectory)> _calls = new();

        public QueueCommandRunner(params CommandResult[] results)
        {
            _results = new Queue<CommandResult>(results);
        }

        public IReadOnlyList<(string FileName, string Arguments, string? WorkingDirectory)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments, executionOptions.WorkingDirectory));
            return Task.FromResult(_results.Count == 0 ? CommandResult.Success(string.Empty) : _results.Dequeue());
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}