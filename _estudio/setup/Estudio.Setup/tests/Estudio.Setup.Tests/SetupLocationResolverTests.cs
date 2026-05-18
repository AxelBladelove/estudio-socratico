using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public sealed class SetupLocationResolverTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-location-{Guid.NewGuid():N}");

    [Fact]
    public void ResolveWorkspaceRoot_prefers_explicit_workspace_root()
    {
        var packageRoot = CreatePackagedInstallerRoot();
        var options = new SetupOptions(SetupMode.Install, WorkspaceRoot: @"C:\Custom\Estudio");

        var resolved = SetupLocationResolver.ResolveWorkspaceRoot(options, packageRoot, packageRoot, "axel");

        Assert.Equal(Path.GetFullPath(@"C:\Custom\Estudio"), resolved);
    }

    [Fact]
    public void ResolveWorkspaceRoot_uses_previous_state_when_packaged_installer_reopens()
    {
        var packageRoot = CreatePackagedInstallerRoot();
        var stateRoot = Path.Combine(_tempRoot, "state");
        Directory.CreateDirectory(stateRoot);
        var previousWorkspace = Path.Combine(_tempRoot, "workspace-real");
        var persistedWorkspace = previousWorkspace.Replace("\\", "\\\\", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(stateRoot, "setup-state.json"), $"{{\"workspace\":\"{persistedWorkspace}\"}}");
        var options = new SetupOptions(SetupMode.Install, StateRoot: stateRoot);

        var resolved = SetupLocationResolver.ResolveWorkspaceRoot(options, packageRoot, packageRoot, "axel");

        Assert.Equal(Path.GetFullPath(previousWorkspace), resolved);
    }

    [Fact]
    public void ResolveWorkspaceRoot_uses_current_workspace_when_running_from_repo()
    {
        var repoRoot = Path.Combine(_tempRoot, "repo");
        Directory.CreateDirectory(Path.Combine(repoRoot, "_estudio", "setup"));
        File.WriteAllText(Path.Combine(repoRoot, "_estudio", "setup", "Estudio.Setup.cmd"), "@echo off");
        var nested = Path.Combine(repoRoot, "Ejercicios");
        Directory.CreateDirectory(nested);

        var resolved = SetupLocationResolver.ResolveWorkspaceRoot(new SetupOptions(SetupMode.Verify), repoRoot, nested, "axel");

        Assert.Equal(repoRoot, resolved);
    }

    private string CreatePackagedInstallerRoot()
    {
        var root = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, SetupPackageLayout.PayloadDirectoryName));
        File.WriteAllText(
            Path.Combine(root, SetupPackageLayout.PayloadDirectoryName, SetupPackageLayout.ManifestFileName),
            "{}");
        return root;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}