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
    public async Task Manifest_SavesLocalAlias()
    {
        var workspace = CreateMinimalWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var manifestManager = new ManifestManager(paths);
        var manager = new WorkspaceManager(paths, manifestManager, new LogManager(paths));

        await manager.PrepareAsync(workspace, "Ana Maria", CancellationToken.None);
        var manifest = await manifestManager.LoadAsync(CancellationToken.None);

        Assert.Equal("ana-maria", manifest.LocalAlias);
        Assert.False(manifest.BuildFlowValidated);
        Assert.Null(manifest.BuildFlowValidatedAtUtc);
    }

    [Fact]
    public void DefaultWorkspace_UsesUserProfileAndAlias()
    {
        var paths = new AppPaths(localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var actual = paths.GetRecommendedWorkspacePath("Ana Maria");

        Assert.EndsWith("Estudio-Socratico-ana-maria", actual);
        Assert.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), actual);
    }

    [Fact]
    public void DefaultWorkspace_RecalculatesWhenAliasChanges()
    {
        var paths = new AppPaths(localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var first = paths.GetRecommendedWorkspacePath("axel");
        var second = paths.GetRecommendedWorkspacePath("Ana Maria");

        Assert.NotEqual(first, second);
        Assert.EndsWith("Estudio-Socratico-ana-maria", second);
    }

    [Fact]
    public void Alias_IsSlugNormalized()
    {
        Assert.Equal("ana-maria", LocalAliasNormalizer.Normalize("Ana Maria"));
        Assert.Equal("axel-dev", LocalAliasNormalizer.Normalize("Axel__Dev"));
    }

    [Fact]
    public async Task ExtensionConfig_CreatesLocalJsonIfMissing()
    {
        var workspace = CreateMinimalWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var manager = new WorkspaceManager(paths, new ManifestManager(paths), new LogManager(paths));

        await manager.PrepareAsync(workspace, "axel", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(workspace, "usuario", "config", "estudio-socratico.extension.local.json")));
        Assert.True(File.Exists(Path.Combine(workspace, "usuario", "config", "estudio-socratico.extension.example.json")));
    }

    [Fact]
    public async Task ExtensionConfig_DoesNotOverwriteExistingLocalJson()
    {
        var workspace = CreateMinimalWorkspace();
        var configDir = Path.Combine(workspace, "usuario", "config");
        Directory.CreateDirectory(configDir);
        var localConfigPath = Path.Combine(configDir, "estudio-socratico.extension.local.json");
        await File.WriteAllTextAsync(localConfigPath, "{\n  \"apiKey\": \"persist-me\"\n}\n");
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var manager = new WorkspaceManager(paths, new ManifestManager(paths), new LogManager(paths));

        await manager.PrepareAsync(workspace, "axel", CancellationToken.None);

        var content = await File.ReadAllTextAsync(localConfigPath);
        Assert.Contains("persist-me", content);
    }

    [Fact]
    public async Task ExtensionConfig_LocalJsonIsGitIgnored()
    {
        var workspace = CreateMinimalWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var manager = new WorkspaceManager(paths, new ManifestManager(paths), new LogManager(paths));

        await manager.PrepareAsync(workspace, "axel", CancellationToken.None);

        var gitignore = await File.ReadAllTextAsync(Path.Combine(workspace, ".gitignore"));
        Assert.Contains("usuario/config/estudio-socratico.extension.local.json", gitignore);
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

        var result = await uninstall.UninstallAsync(false, CancellationToken.None);

        Assert.True(result.ManifestFound);
        Assert.False(Directory.Exists(managed));
        Assert.True(Directory.Exists(outside));
        Assert.Contains(outside, result.SkippedPaths);
    }

    [Fact]
    public async Task Uninstall_Preserves_Exercises_Logs_And_User_By_Default()
    {
        var workspace = CreateMinimalWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, "Ejercicios"));
        Directory.CreateDirectory(Path.Combine(workspace, "usuario", "logs", "main"));
        File.WriteAllText(Path.Combine(workspace, "Ejercicios", "main.c"), "int main(void){return 0;}");
        File.WriteAllText(Path.Combine(workspace, "usuario", "logs", "main", "bloque1.log"), "log real");

        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var managed = Path.Combine(root, "Tools", "bin");
        Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "tool.exe"), "managed-test-binary");

        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: root);
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest { WorkspacePath = workspace, SafeToRemove = { managed } });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.UninstallAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.True(result.ManifestFound);
        Assert.False(result.WorkspaceRemoved);
        Assert.False(Directory.Exists(managed));
        Assert.True(File.Exists(Path.Combine(workspace, "Ejercicios", "main.c")));
        Assert.True(File.Exists(Path.Combine(workspace, "usuario", "logs", "main", "bloque1.log")));
        Assert.True(Directory.Exists(Path.Combine(workspace, "usuario")));
    }

    [Fact]
    public async Task Uninstall_Without_Manifest_Uses_Safe_Mode()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(localAppDataRoot: root);
        var uninstall = new UninstallManager(paths, new ManifestManager(paths), new LogManager(paths), new SecurityManager());

        var result = await uninstall.UninstallAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.False(result.ManifestFound);
        Assert.Empty(result.RemovedPaths);
    }

    [Fact]
    public async Task Uninstall_DryRun_DoesNotDeleteFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var managedFile = Path.Combine(root, "Tools", "bin", "exercism.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(managedFile)!);
        await File.WriteAllTextAsync(managedFile, "managed");

        var paths = new AppPaths(localAppDataRoot: root);
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest { SafeToRemove = { managedFile } });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.PreviewAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.True(result.DryRun);
        Assert.True(File.Exists(managedFile));
        Assert.Contains(managedFile, result.WouldRemovePaths);
        Assert.Empty(result.RemovedPaths);
    }

    [Fact]
    public async Task Uninstall_KeepsStudentData()
    {
        var workspace = CreateMinimalWorkspace();
        var exercise = Path.Combine(workspace, "Ejercicios", "main.c");
        var userLog = Path.Combine(workspace, "usuario", "logs", "main", "bloque1.log");
        Directory.CreateDirectory(Path.GetDirectoryName(exercise)!);
        Directory.CreateDirectory(Path.GetDirectoryName(userLog)!);
        await File.WriteAllTextAsync(exercise, "int main(void){return 0;}");
        await File.WriteAllTextAsync(userLog, "log");

        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: root);
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            SafeToRemove = { exercise, Path.Combine(workspace, "usuario") }
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.PreviewAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.True(File.Exists(exercise));
        Assert.True(File.Exists(userLog));
        Assert.Contains(result.KeptPaths, path => path.EndsWith("Ejercicios", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.KeptPaths, path => path.EndsWith("usuario", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(exercise, result.WouldRemovePaths);
    }

    [Fact]
    public async Task Uninstall_KeepsApiKeyConfig()
    {
        var workspace = CreateMinimalWorkspace();
        var apiKeyPath = Path.Combine(workspace, "usuario", "config", "estudio-socratico.extension.local.json");
        Directory.CreateDirectory(Path.GetDirectoryName(apiKeyPath)!);
        await File.WriteAllTextAsync(apiKeyPath, """{"apiKey":"keep-me"}""");

        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: root);
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            SafeToRemove = { apiKeyPath }
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.PreviewAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.True(File.Exists(apiKeyPath));
        Assert.Contains(apiKeyPath, result.KeptPaths);
        Assert.DoesNotContain(apiKeyPath, result.WouldRemovePaths);
    }

    [Fact]
    public async Task Uninstall_SkipsUnsafePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N"), "tool.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        await File.WriteAllTextAsync(outside, "not-managed");

        var paths = new AppPaths(localAppDataRoot: root);
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest { SafeToRemove = { outside } });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.PreviewAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.True(File.Exists(outside));
        Assert.Contains(outside, result.SkippedPaths);
        Assert.Contains(result.Items, item => item.Path == outside && item.Action == "skipped");
    }

    [Fact]
    public async Task Uninstall_RequiresManifestForManagedTools()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var managedFile = Path.Combine(root, "Tools", "bin", "exercism.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(managedFile)!);
        await File.WriteAllTextAsync(managedFile, "managed");

        var paths = new AppPaths(localAppDataRoot: root);
        var uninstall = new UninstallManager(paths, new ManifestManager(paths), new LogManager(paths), new SecurityManager());

        var result = await uninstall.PreviewAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.False(result.ManifestFound);
        Assert.True(File.Exists(managedFile));
        Assert.Empty(result.WouldRemovePaths);
        Assert.Contains(root, result.SkippedPaths);
    }

    [Fact]
    public async Task Uninstall_ReportListsRemovedAndKeptItems()
    {
        var workspace = CreateMinimalWorkspace();
        var root = Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N"));
        var managedDir = Path.Combine(root, "Tools", "exercism");
        Directory.CreateDirectory(managedDir);
        await File.WriteAllTextAsync(Path.Combine(managedDir, "exercism.exe"), "managed");

        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: root);
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            SafeToRemove = { managedDir }
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.PreviewAsync(allowAggressiveCleanup: false, CancellationToken.None);

        Assert.Contains(managedDir, result.WouldRemovePaths);
        Assert.Contains(workspace, result.KeptPaths);
        Assert.Contains(result.Items, item => item.Action == "wouldRemove" && item.Path == managedDir);
        Assert.Contains(result.Items, item => item.Action == "kept" && item.Path == workspace);
    }

    private static string CreateMinimalWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "include"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "exercism"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "vscode", "estudio-exercism"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# test");
        File.WriteAllText(Path.Combine(root, ".gitignore"), "bin/\n");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "build.cmd"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "compilar_y_grabar.bat"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "include", "conio.h"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "exercism", "manager.ps1"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "vscode", "estudio-exercism", "package.json"), """{"name":"estudio-exercism","publisher":"estudio-socratico","version":"1.0.0"}""");
        File.WriteAllText(Path.Combine(root, "_estudio", "errores.template.md"), "# Errores");
        return root;
    }
}
