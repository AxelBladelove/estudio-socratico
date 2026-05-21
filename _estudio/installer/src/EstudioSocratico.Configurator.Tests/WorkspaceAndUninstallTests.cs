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

        await manager.PrepareAsync(workspace, "Ana Maria", CancellationToken.None, "anamaria");

        Assert.True(File.Exists(Path.Combine(workspace, "usuario", "errores.md")));
        Assert.Equal("ana-maria", File.ReadAllText(Path.Combine(workspace, ".estudio_usuario")));
        var identity = WorkspaceIdentityStore.Read(workspace);
        Assert.Equal("ana-maria", identity?.Alias);
        Assert.Equal("anamaria", identity?.GitHubLogin);
        Assert.Equal("anamaria/estudio-socratico-ana-maria", identity?.WorkspaceRepo);
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
        Workspace_DefaultUsesAlias();
    }

    [Fact]
    public void Workspace_DefaultUsesAlias()
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
    public async Task UpdatePreservesAliasIdentity()
    {
        var workspace = CreateMinimalWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var manager = new WorkspaceManager(paths, new ManifestManager(paths), new LogManager(paths));
        await manager.PrepareAsync(workspace, "erick", CancellationToken.None, "ericgabriel");

        await manager.PrepareAsync(workspace, "erick", CancellationToken.None, "ericgabriel");

        var identity = WorkspaceIdentityStore.Read(workspace);
        Assert.Equal("erick", identity?.Alias);
        Assert.Equal("ericgabriel", identity?.GitHubLogin);
        Assert.Equal("ericgabriel/estudio-socratico-erick", identity?.WorkspaceRepo);
    }

    [Fact]
    public async Task ReinstallPreservesAliasIdentity()
    {
        var workspace = CreateMinimalWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var manager = new WorkspaceManager(paths, new ManifestManager(paths), new LogManager(paths));
        await manager.PrepareAsync(workspace, "erick", CancellationToken.None, "ericgabriel");
        var localConfig = Path.Combine(workspace, "usuario", "config", "estudio-socratico.extension.local.json");
        await File.WriteAllTextAsync(localConfig, "{\n  \"apiKey\": \"keep-me\"\n}\n");

        await manager.PrepareAsync(workspace, "erick", CancellationToken.None, "ericgabriel");

        Assert.Equal("erick", File.ReadAllText(Path.Combine(workspace, ".estudio_usuario")));
        Assert.Contains("keep-me", await File.ReadAllTextAsync(localConfig));
        Assert.Equal("ericgabriel", WorkspaceIdentityStore.Read(workspace)?.GitHubLogin);
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

    [Fact]
    public async Task Reinstall_Default_KeepsIdentityAndLogs()
    {
        var workspace = await CreateStudentWorkspaceAsync("testfork", "AxelBladelove");
        var logPath = Path.Combine(workspace, "usuario", "logs", "main", "bloque1.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.WriteAllTextAsync(logPath, "keep");
        var paths = new AppPaths(repoRoot: CreateMinimalWorkspace(), localAppDataRoot: Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N")));
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            LocalAlias = "testfork",
            GitHub = new AccountState { Configured = true, UserName = "AxelBladelove" },
            WorkspaceRepo = "AxelBladelove/estudio-socratico-testfork",
            WorkspaceRepoCreatedByEstudio = true
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.UninstallAsync(allowAggressiveCleanup: false, dryRun: true, CancellationToken.None);

        Assert.False(result.WorkspaceRemoved);
        Assert.Contains(workspace, result.KeptPaths);
        Assert.True(File.Exists(Path.Combine(workspace, ".usuario")));
        Assert.True(File.Exists(logPath));
    }

    [Fact]
    public async Task Reinstall_Clean_RemovesIdentityLogsAndWorkspace()
    {
        var workspace = await CreateStudentWorkspaceAsync("testfork", "AxelBladelove");
        var paths = new AppPaths(repoRoot: CreateMinimalWorkspace(), localAppDataRoot: Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N")));
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            LocalAlias = "testfork",
            GitHub = new AccountState { Configured = true, UserName = "AxelBladelove" }
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.UninstallAsync(false, dryRun: false, deleteStudentData: true, deleteRemoteWorkspaceRepo: false, CancellationToken.None);

        Assert.True(result.WorkspaceRemoved);
        Assert.False(Directory.Exists(workspace));
        Assert.Contains(result.RemovedPaths, path => path.EndsWith("Estudio-Socratico-testfork", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reinstall_Clean_RecreatesWorkspaceRepo()
    {
        var workspace = await CreateStudentWorkspaceAsync("testfork", "AxelBladelove");
        var paths = new AppPaths(repoRoot: CreateMinimalWorkspace(), localAppDataRoot: Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N")));
        var runner = new RecordingRunner();
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            LocalAlias = "testfork",
            GitHub = new AccountState { Configured = true, UserName = "AxelBladelove" },
            WorkspaceRepo = "AxelBladelove/estudio-socratico-testfork",
            WorkspaceRepoCreatedByEstudio = true
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager(), runner);

        var result = await uninstall.UninstallAsync(false, dryRun: true, deleteStudentData: true, deleteRemoteWorkspaceRepo: true, CancellationToken.None);

        Assert.Contains("github:AxelBladelove/estudio-socratico-testfork", result.WouldRemovePaths);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task Uninstall_Default_KeepsStudentData()
    {
        await Uninstall_KeepsStudentData();
    }

    [Fact]
    public async Task Uninstall_DeleteStudentData_RemovesWorkspaceAndConfig()
    {
        var workspace = await CreateStudentWorkspaceAsync("testfork", "AxelBladelove");
        var paths = new AppPaths(repoRoot: CreateMinimalWorkspace(), localAppDataRoot: Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N")));
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            LocalAlias = "testfork",
            GitHub = new AccountState { Configured = true, UserName = "AxelBladelove" }
        });
        Assert.True(File.Exists(paths.ManifestPath));
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager());

        var result = await uninstall.UninstallAsync(false, dryRun: false, deleteStudentData: true, deleteRemoteWorkspaceRepo: false, CancellationToken.None);

        Assert.True(result.WorkspaceRemoved);
        Assert.False(Directory.Exists(workspace));
        Assert.False(File.Exists(paths.ManifestPath));
        Assert.False(Directory.Exists(paths.LogsRoot));
    }

    [Fact]
    public async Task Uninstall_DeleteRemoteRepo_OnlyDeletesWorkspaceRepo()
    {
        var workspace = await CreateStudentWorkspaceAsync("testfork", "AxelBladelove");
        var paths = new AppPaths(repoRoot: CreateMinimalWorkspace(), localAppDataRoot: Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N")));
        var runner = new RecordingRunner();
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            LocalAlias = "testfork",
            GitHub = new AccountState { Configured = true, UserName = "AxelBladelove" },
            WorkspaceRepo = "AxelBladelove/estudio-socratico-testfork",
            WorkspaceRepoCreatedByEstudio = true
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager(), runner);

        var result = await uninstall.UninstallAsync(false, dryRun: false, deleteStudentData: false, deleteRemoteWorkspaceRepo: true, CancellationToken.None);

        Assert.Contains("github:AxelBladelove/estudio-socratico-testfork", result.RemovedPaths);
        Assert.Contains(runner.Commands, spec => spec.FileName == "gh" && spec.Arguments.SequenceEqual(["repo", "delete", "AxelBladelove/estudio-socratico-testfork", "--yes"]));
        Assert.DoesNotContain(runner.Commands, spec => spec.Arguments.Contains(ProductInfo.BaseRepository));
    }

    [Fact]
    public async Task Uninstall_NeverDeletesBaseRepo()
    {
        var workspace = await CreateStudentWorkspaceAsync("testfork", "AxelBladelove");
        var paths = new AppPaths(repoRoot: CreateMinimalWorkspace(), localAppDataRoot: Path.Combine(Path.GetTempPath(), "estudio-uninstall-" + Guid.NewGuid().ToString("N")));
        var runner = new RecordingRunner();
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            LocalAlias = "testfork",
            GitHub = new AccountState { Configured = true, UserName = "AxelBladelove" },
            WorkspaceRepo = ProductInfo.BaseRepository,
            WorkspaceRepoCreatedByEstudio = true
        });
        var uninstall = new UninstallManager(paths, manifestManager, new LogManager(paths), new SecurityManager(), runner);

        var result = await uninstall.UninstallAsync(false, dryRun: false, deleteStudentData: false, deleteRemoteWorkspaceRepo: true, CancellationToken.None);

        Assert.Empty(runner.Commands);
        Assert.DoesNotContain(result.RemovedPaths, path => path.Contains(ProductInfo.BaseRepository, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Items, item => item.Action == "skipped" && item.Path == "github:workspaceRepo");
    }

    private static async Task<string> CreateStudentWorkspaceAsync(string alias, string githubLogin)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "estudio-student-" + Guid.NewGuid().ToString("N"),
            $"{ProductInfo.DefaultWorkspaceFolderPrefix}-{LocalAliasNormalizer.Normalize(alias)}");
        Directory.CreateDirectory(Path.Combine(root, "usuario", "config"));
        Directory.CreateDirectory(Path.Combine(root, "usuario", "logs"));
        Directory.CreateDirectory(Path.Combine(root, "Ejercicios"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# test");
        File.WriteAllText(Path.Combine(root, "usuario", "config", "estudio-socratico.extension.local.json"), """{"apiKey":"keep"}""");
        await WorkspaceIdentityStore.WriteAsync(root, alias, githubLogin, CancellationToken.None);
        return root;
    }

    private sealed class RecordingRunner : ICommandRunner
    {
        public List<CommandSpec> Commands { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            Commands.Add(spec);
            return Task.FromResult(new CommandResult
            {
                Spec = spec,
                ExitCode = 0,
                StandardOutput = "",
                StandardError = ""
            });
        }
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
