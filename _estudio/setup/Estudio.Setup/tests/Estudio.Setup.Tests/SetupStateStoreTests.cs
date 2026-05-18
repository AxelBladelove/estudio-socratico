using System.Text.Json;
using Estudio.Setup.Core;
using Estudio.Setup.State;

namespace Estudio.Setup.Tests;

public class SetupStateStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "EstudioSetupTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_writes_setup_state_json_with_mode_and_components()
    {
        var store = new FileSetupStateStore(_tempRoot);
        var report = SetupReport.Passed(new[]
        {
            new StepExecution("git", "verify", StepResult.Ok("git ok")),
            new StepExecution("gcc", "verify", StepResult.Ok("gcc ok")),
        });

        var path = await store.SaveAsync(new SetupOptions(SetupMode.Verify), report, CancellationToken.None);

        Assert.Equal(Path.Combine(_tempRoot, "setup-state.json"), path);
        Assert.True(File.Exists(path));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = document.RootElement;

        Assert.Equal(FileSetupStateStore.CurrentSetupVersion, root.GetProperty("setupVersion").GetString());
        Assert.Equal("Verify", root.GetProperty("mode").GetString());
        Assert.Equal("verify-final", root.GetProperty("lastSuccessfulStep").GetString());
        Assert.Equal("ok", root.GetProperty("installedComponents").GetProperty("git").GetString());
        Assert.Equal("ok", root.GetProperty("installedComponents").GetProperty("gcc").GetString());
    }

    [Fact]
    public async Task SaveAsync_records_latest_result_for_detect_only_components()
    {
        var store = new FileSetupStateStore(_tempRoot);
        var report = SetupReport.Failed("git", new[]
        {
            new StepExecution("git", "detect", StepResult.Ok("git ok")),
            new StepExecution("git", "verify", StepResult.Ok("git ok")),
            new StepExecution("github-fork", "detect", StepResult.Missing("fork missing")),
            new StepExecution("git-safety-backup", "detect", StepResult.Warning("dirty worktree")),
            new StepExecution("vscode-settings", "detect", StepResult.Fail("invalid json")),
        });

        var path = await store.SaveAsync(new SetupOptions(SetupMode.Verify), report, CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var components = document.RootElement.GetProperty("installedComponents");
        Assert.Equal("ok", components.GetProperty("git").GetString());
        Assert.Equal("missing", components.GetProperty("github-fork").GetString());
        Assert.Equal("warning", components.GetProperty("git-safety-backup").GetString());
        Assert.Equal("failed", components.GetProperty("vscode-settings").GetString());
    }

    [Fact]
    public async Task SaveAsync_writes_identity_and_workspace_metadata_when_provided()
    {
        var store = new FileSetupStateStore(_tempRoot);
        var report = SetupReport.Passed(new[]
        {
            new StepExecution("git", "verify", StepResult.Ok("git ok")),
        });
        var metadata = new SetupStateMetadata(
            Alias: "axel",
            GithubUser: "AxelBladelove",
            ForkOwner: "AxelBladelove",
            ForkName: "estudio-socratico-axel",
            Upstream: "AxelBladelove/estudio-socratico",
            Workspace: @"C:\repo\estudio-socratico");

        var path = await store.SaveAsync(new SetupOptions(SetupMode.Verify), report, metadata, CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = document.RootElement;
        Assert.Equal("axel", root.GetProperty("alias").GetString());
        Assert.Equal("AxelBladelove", root.GetProperty("githubUser").GetString());
        Assert.Equal("AxelBladelove", root.GetProperty("forkOwner").GetString());
        Assert.Equal("estudio-socratico-axel", root.GetProperty("forkName").GetString());
        Assert.Equal("AxelBladelove/estudio-socratico", root.GetProperty("upstream").GetString());
        Assert.Equal(@"C:\repo\estudio-socratico", root.GetProperty("workspace").GetString());
    }

    [Fact]
    public void ForWorkspace_builds_expected_fork_metadata_from_alias_and_github_user()
    {
        var metadata = SetupStateMetadata.ForWorkspace("axel", @"C:\repo\estudio-socratico", "AxelBladelove");

        Assert.Equal("axel", metadata.Alias);
        Assert.Equal("AxelBladelove", metadata.GithubUser);
        Assert.Equal("AxelBladelove", metadata.ForkOwner);
        Assert.Equal("estudio-socratico-axel", metadata.ForkName);
        Assert.Equal("AxelBladelove/estudio-socratico", metadata.Upstream);
        Assert.Equal(@"C:\repo\estudio-socratico", metadata.Workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
