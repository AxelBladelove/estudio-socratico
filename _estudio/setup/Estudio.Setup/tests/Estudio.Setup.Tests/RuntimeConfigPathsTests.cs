using Estudio.Setup.Runtime;

namespace Estudio.Setup.Tests;

public class RuntimeConfigPathsTests
{
    [Fact]
    public void ResolveWorkspaceRuntimeConfigPath_returns_private_setup_config_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));

        var resolved = RuntimeConfigPaths.ResolveWorkspaceRuntimeConfigPath(root);

        Assert.Equal(Path.Combine(root, "_estudio", "setup", "runtime-config.private.json"), resolved);
    }

    [Fact]
    public void ResolveBundledRuntimeConfigBootstrapPath_returns_bootstrap_next_to_setup()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));

        var resolved = RuntimeConfigPaths.ResolveBundledRuntimeConfigBootstrapPath(root);

        Assert.Equal(Path.Combine(root, "runtime-config.bootstrap.json"), resolved);
    }

    [Fact]
    public void ResolveWorkspaceRuntimeConfigBootstrapPath_returns_private_setup_bootstrap_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));

        var resolved = RuntimeConfigPaths.ResolveWorkspaceRuntimeConfigBootstrapPath(root);

        Assert.Equal(Path.Combine(root, "_estudio", "setup", "runtime-config.bootstrap.json"), resolved);
    }
}
