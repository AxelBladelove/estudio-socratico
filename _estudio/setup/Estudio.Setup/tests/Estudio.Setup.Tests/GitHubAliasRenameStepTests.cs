using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public sealed class GitHubAliasRenameStepTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"estudio-alias-rename-{Guid.NewGuid():N}");

    [Fact]
    public async Task UpdateAsync_renames_existing_old_fork_when_alias_changed_and_target_is_free()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, ".estudio_usuario"), "oldalias");
        var runner = new QueueCommandRunner(
            CommandResult.Success("octocat"),
            CommandResult.Success("estudio-socratico-oldalias"),
            CommandResult.Failure(1, string.Empty, "not found"),
            CommandResult.Success("renamed"));
        var step = new GitHubAliasRenameStep(runner, _root, "newalias");

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update, AliasOverride: "newalias")), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "api user --jq .login"), runner.Calls[0]);
        Assert.Equal(("gh", "repo view octocat/estudio-socratico-oldalias --json name --jq .name"), runner.Calls[1]);
        Assert.Equal(("gh", "repo view octocat/estudio-socratico-newalias --json name --jq .name"), runner.Calls[2]);
        Assert.Equal(("gh", "repo rename estudio-socratico-newalias --repo octocat/estudio-socratico-oldalias --yes"), runner.Calls[3]);
    }

    [Fact]
    public async Task UpdateAsync_uses_existing_target_fork_without_renaming_old_fork()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, ".estudio_usuario"), "oldalias");
        var runner = new QueueCommandRunner(
            CommandResult.Success("octocat"),
            CommandResult.Success("estudio-socratico-oldalias"),
            CommandResult.Success("estudio-socratico-newalias"));
        var step = new GitHubAliasRenameStep(runner, _root, "newalias");

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update, AliasOverride: "newalias")), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("ya existe", result.Message);
        Assert.Equal(3, runner.Calls.Count);
    }

    [Fact]
    public async Task DetectAsync_is_ok_when_alias_did_not_change()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, ".estudio_usuario"), "axel");
        var runner = new QueueCommandRunner();
        var step = new GitHubAliasRenameStep(runner, _root, "axel");

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task VerifyAsync_succeeds_when_target_fork_exists_after_alias_change()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, ".estudio_usuario"), "oldalias");
        var runner = new QueueCommandRunner(
            CommandResult.Success("octocat"),
            CommandResult.Success("estudio-socratico-newalias"));
        var step = new GitHubAliasRenameStep(runner, _root, "newalias");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Update, AliasOverride: "newalias")), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("gh", "api user --jq .login"), runner.Calls[0]);
        Assert.Equal(("gh", "repo view octocat/estudio-socratico-newalias --json name --jq .name"), runner.Calls[1]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
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
