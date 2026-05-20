using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class ManifestTests
{
    [Fact]
    public async Task Saves_And_Loads_Manifest()
    {
        var temp = Path.Combine(Path.GetTempPath(), "estudio-manifest-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(localAppDataRoot: temp);
        var manager = new ManifestManager(paths);
        var manifest = new InstallerManifest { WorkspacePath = "workspace" };

        await manager.SaveAsync(manifest);
        var loaded = await manager.LoadAsync();

        Assert.Equal("workspace", loaded.WorkspacePath);
        Assert.True(File.Exists(paths.ManifestPath));
    }
}
