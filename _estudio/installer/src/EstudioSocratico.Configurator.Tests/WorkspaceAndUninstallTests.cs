using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class WorkspaceAndUninstallTests
{
    [Fact]
    public async Task Workspace_Prepare_Creates_User_Errors_Without_Removing_Data()
    {
        var workspace = CreateMinimalWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var manager = new WorkspaceManager(paths, new ManifestManager(paths), new LogManager(paths));

        await manager.PrepareAsync(workspace, "Ana Maria", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(workspace, "usuario", "errores.md")));
        Assert.Equal("ana-maria", File.ReadAllText(Path.Combine(workspace, ".estudio_usuario")));
    }

    [Fact]
    public async Task Uninstall_Skips_Unsafe_Paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var managed = Path.Combine(root, "Tools", "bin");
        Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "tool.exe"), "managed-test-binary");
        var outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);

        var paths = new AppPaths(localAppDataRoot: root);
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest { SafeToRemove = { managed, outside } });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        await uninstall.UninstallAsync(false, CancellationToken.None);

        Assert.False(Directory.Exists(managed));
        Assert.True(Directory.Exists(outside));
    }

    private static string CreateMinimalWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "include"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "exercism"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# test");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "build.cmd"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "compilar_y_grabar.bat"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "include", "conio.h"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "exercism", "manager.ps1"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "errores.template.md"), "# Errores");
        return root;
    }
}
