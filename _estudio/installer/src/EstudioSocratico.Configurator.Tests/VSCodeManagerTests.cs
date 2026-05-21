using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class VSCodeManagerTests
{
    [Fact]
    public async Task VSCodeDetector_no_ejecuta_bin_code_sin_extension()
    {
        var root = NewTemp();
        var install = Path.Combine(root, "Microsoft VS Code");
        Directory.CreateDirectory(Path.Combine(install, "bin"));
        var codeExe = Path.Combine(install, "Code.exe");
        var binCode = Path.Combine(install, "bin", "code");
        File.WriteAllText(codeExe, "");
        File.WriteAllText(binCode, "");
        var runner = new RecordingRunner(spec => RecordingRunner.Result(spec, 0, "1.100.0"));
        var codeCmd = Path.Combine(install, "bin", "code.cmd");
        File.WriteAllText(codeCmd, "");
        var detector = new DependencyDetector(runner, () => new VSCodePaths(codeExe, codeCmd));

        _ = await detector.DetectAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.VSCode));

        Assert.DoesNotContain(runner.Specs, spec => spec.FileName.EndsWith(Path.Combine("bin", "code"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VSCodeDetector_prefiere_CodeExe()
    {
        var root = NewTemp();
        var install = Path.Combine(root, "Microsoft VS Code");
        Directory.CreateDirectory(Path.Combine(install, "bin"));
        var codeExe = Path.Combine(install, "Code.exe");
        var codeCmd = Path.Combine(install, "bin", "code.cmd");
        File.WriteAllText(codeExe, "");
        File.WriteAllText(codeCmd, "");
        var runner = new RecordingRunner(spec => RecordingRunner.Result(spec, 0, "1.100.0"));
        var detector = new DependencyDetector(runner, () => VSCodeLocator.ResolveFromInstallRoots([install]));

        var state = await detector.DetectAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.VSCode));

        Assert.Equal(DependencyStatus.Ready, state.Status);
        Assert.Equal(codeExe, state.Path);
        Assert.Equal("cmd.exe", Assert.Single(runner.Specs).FileName);
    }

    [Fact]
    public void VSCodeDetector_usa_codeCmd_con_cmdExe()
    {
        var spec = VSCodeLocator.BuildCodeCmdCommand(
            @"C:\Users\test\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd",
            ["--install-extension", "ms-vscode.cpptools", "--force"],
            @"C:\workspace",
            TimeSpan.FromMinutes(4));

        Assert.Equal("cmd.exe", spec.FileName);
        Assert.NotNull(spec.ArgumentString);
        Assert.Contains("/c", spec.ArgumentString);
        Assert.Contains("code.cmd", spec.ArgumentString);
        Assert.Contains("--install-extension", spec.ArgumentString);
    }

    [Fact]
    public async Task OpenVSCode_usa_workspace_como_workingDirectory()
    {
        var root = NewTemp();
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var codeExe = Path.Combine(root, "Code.exe");
        File.WriteAllText(codeExe, "");
        var runner = new RecordingRunner(spec => RecordingRunner.Result(spec, 0, ""));
        var manager = CreateManager(root, runner, () => new VSCodePaths(codeExe, null));

        await manager.OpenWorkspaceAsync(workspace, CancellationToken.None);

        var spec = Assert.Single(runner.Specs);
        Assert.Equal(codeExe, spec.FileName);
        Assert.Equal(workspace, spec.WorkingDirectory);
        Assert.Equal(workspace, Assert.Single(spec.Arguments));
    }

    [Fact]
    public async Task InstallVsCodeExtensions_no_declara_exito_si_code_falla()
    {
        var root = NewTemp();
        var workspace = CreateWorkspaceWithExtension(root);
        var codeExe = Path.Combine(root, "Code.exe");
        var codeCmd = Path.Combine(root, "code.cmd");
        File.WriteAllText(codeExe, "");
        File.WriteAllText(codeCmd, "");
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "local"));
        var logManager = new LogManager(paths);
        var runner = new RecordingRunner(spec =>
            spec.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)
                ? RecordingRunner.Result(spec, 1, "", "extension failed")
                : RecordingRunner.Result(spec, 0, "1.100.0"));
        var manager = new VSCodeManager(
            runner,
            new ExtensionManager(paths, logManager, userProfileRoot: root),
            new ManifestManager(paths),
            logManager,
            () => new VSCodePaths(codeExe, codeCmd));

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(workspace, CancellationToken.None));

        var log = File.Exists(logManager.InstallerLogPath)
            ? await File.ReadAllTextAsync(logManager.InstallerLogPath)
            : "";
        Assert.DoesNotContain("VS Code preparado", log);
    }

    [Fact]
    public async Task VSCodeExtension_IsInstalledOrReinstalled()
    {
        var root = NewTemp();
        var workspace = CreateWorkspaceWithExtension(root);
        var codeExe = Path.Combine(root, "Code.exe");
        var codeCmd = Path.Combine(root, "code.cmd");
        File.WriteAllText(codeExe, "");
        File.WriteAllText(codeCmd, "");
        var stalePath = Path.Combine(root, ".vscode", "extensions", "estudio-socratico.estudio-exercism-0.9.0");
        Directory.CreateDirectory(stalePath);
        File.WriteAllText(Path.Combine(stalePath, "stale.txt"), "old");
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "local"));
        var logManager = new LogManager(paths);
        var runner = new RecordingRunner(spec =>
        {
            if (spec.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                (spec.ArgumentString?.Contains("--list-extensions", StringComparison.OrdinalIgnoreCase) == true))
            {
                return RecordingRunner.Result(spec, 0, "estudio-socratico.estudio-exercism@1.0.0");
            }

            return RecordingRunner.Result(spec, 0, "1.100.0");
        });
        var extensionManager = new ExtensionManager(paths, logManager, userProfileRoot: root);
        var manager = new VSCodeManager(
            runner,
            extensionManager,
            new ManifestManager(paths),
            logManager,
            () => new VSCodePaths(codeExe, codeCmd));

        await manager.PrepareAsync(workspace, CancellationToken.None);
        var state = await manager.DiagnoseExtensionAsync(workspace, CancellationToken.None);

        Assert.Equal(ResourceStatus.Ready, state.Status);
        Assert.True(state.InstalledInVSCode);
        Assert.True(state.ActivityBarConfigured);
        Assert.True(state.CommandsRegistered);
        Assert.True(state.ExercisePanelAvailable);
        Assert.True(state.ManagerScriptExists);
        Assert.False(Directory.Exists(stalePath));
        Assert.True(Directory.Exists(Path.Combine(root, ".vscode", "extensions", "estudio-socratico.estudio-exercism-1.0.0")));
    }

    [Fact]
    public async Task VSCodeExtension_Diagnose_Fails_When_InstalledPackageJsonMissing()
    {
        var root = NewTemp();
        var workspace = CreateWorkspaceWithExtension(root);
        var codeExe = Path.Combine(root, "Code.exe");
        var codeCmd = Path.Combine(root, "code.cmd");
        File.WriteAllText(codeExe, "");
        File.WriteAllText(codeCmd, "");
        var brokenInstall = Path.Combine(root, ".vscode", "extensions", "estudio-socratico.estudio-exercism-1.0.0");
        Directory.CreateDirectory(brokenInstall);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "local"));
        var logManager = new LogManager(paths);
        var runner = new RecordingRunner(spec =>
        {
            if (spec.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                (spec.ArgumentString?.Contains("--list-extensions", StringComparison.OrdinalIgnoreCase) == true))
            {
                return RecordingRunner.Result(spec, 0, "estudio-socratico.estudio-exercism@1.0.0");
            }

            return RecordingRunner.Result(spec, 0, "1.100.0");
        });
        var manager = new VSCodeManager(
            runner,
            new ExtensionManager(paths, logManager, userProfileRoot: root),
            new ManifestManager(paths),
            logManager,
            () => new VSCodePaths(codeExe, codeCmd));

        var state = await manager.DiagnoseExtensionAsync(workspace, CancellationToken.None);

        Assert.Equal(ResourceStatus.Broken, state.Status);
        Assert.True(state.InstalledInVSCode);
        Assert.Contains("package.json", state.HumanDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenExercisePanel_Fails_When_LocalExtension_IsNotInstalled()
    {
        var root = NewTemp();
        var workspace = CreateWorkspaceWithExtension(root);
        var codeExe = Path.Combine(root, "Code.exe");
        File.WriteAllText(codeExe, "");
        var runner = new RecordingRunner(spec => RecordingRunner.Result(spec, 0, ""));
        var manager = CreateManager(root, runner, () => new VSCodePaths(codeExe, null));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.OpenExercisePanelAsync(workspace, CancellationToken.None));

        Assert.Contains("no aparece instalada", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static VSCodeManager CreateManager(string root, ICommandRunner runner, Func<VSCodePaths> locator)
    {
        var paths = new AppPaths(localAppDataRoot: Path.Combine(root, "local"));
        var logManager = new LogManager(paths);
        return new VSCodeManager(runner, new ExtensionManager(paths, logManager, userProfileRoot: root), new ManifestManager(paths), logManager, locator);
    }

    private static string CreateWorkspaceWithExtension(string root)
    {
        var workspace = Path.Combine(root, "workspace");
        var extension = Path.Combine(workspace, "_estudio", "soporte", "vscode", "estudio-exercism");
        Directory.CreateDirectory(extension);
        Directory.CreateDirectory(Path.Combine(workspace, "_estudio", "soporte", "exercism"));
        File.WriteAllText(Path.Combine(extension, "package.json"), """
{"name":"estudio-exercism","publisher":"estudio-socratico","version":"1.0.0","contributes":{"viewsContainers":{"activitybar":[{"id":"estudioSocratico","title":"Estudio"}]},"views":{"estudioSocratico":[{"id":"estudioExercism.view","name":"Ejercicios"}]},"commands":[{"command":"estudioExercism.openPanel"},{"command":"estudioExercism.openApiKeyConfig"},{"command":"estudioExercism.revealApiKeyConfig"}]}}
""");
        File.WriteAllText(Path.Combine(workspace, "_estudio", "soporte", "exercism", "manager.ps1"), "");
        return workspace;
    }

    private static string NewTemp()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-vscode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class RecordingRunner(Func<CommandSpec, CommandResult> handler) : ICommandRunner
    {
        public List<CommandSpec> Specs { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            Specs.Add(spec);
            return Task.FromResult(handler(spec));
        }

        public static CommandResult Result(CommandSpec spec, int exitCode, string output = "", string error = "") => new()
        {
            Spec = spec,
            ExitCode = exitCode,
            StandardOutput = output,
            StandardError = error,
            Duration = TimeSpan.FromMilliseconds(1)
        };
    }
}
