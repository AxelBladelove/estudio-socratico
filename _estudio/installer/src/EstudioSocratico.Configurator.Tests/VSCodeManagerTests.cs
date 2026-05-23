using System.Text.Json.Nodes;
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
    public async Task VSCodeExtension_RestartRequired_FallsBackToProfileCopy()
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
        {
            if (spec.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                spec.ArgumentString?.Contains("--install-extension", StringComparison.OrdinalIgnoreCase) == true &&
                spec.ArgumentString.Contains(".vsix", StringComparison.OrdinalIgnoreCase))
            {
                return RecordingRunner.Result(spec, 1, "Installing extensions...", "Error: Please restart VS Code before reinstalling Estudio Socratico - Exercism.");
            }

            if (spec.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                spec.ArgumentString?.Contains("--list-extensions", StringComparison.OrdinalIgnoreCase) == true)
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

        await manager.PrepareAsync(workspace, CancellationToken.None);

        Assert.True(Directory.Exists(Path.Combine(root, ".vscode", "extensions", "estudio-socratico.estudio-exercism-1.0.0")));
        var log = await File.ReadAllTextAsync(logManager.InstallerLogPath);
        Assert.Contains("pidio reinicio", log);
    }

    [Fact]
    public async Task Update_ReinstallsVSCodeExtensionWhenPackagedVersionIsNewer()
    {
        var root = NewTemp();
        var workspace = CreateWorkspaceWithExtension(root, "2.0.0", useLegacyBranding: true);
        var packagedRoot = Path.Combine(root, "packaged-root");
        var packagedWorkspace = CreateWorkspaceWithExtension(packagedRoot, "2.0.16", useLegacyBranding: false);
        var packagedSource = Path.Combine(packagedRoot, "_estudio", "soporte", "vscode", "estudio-exercism");
        Directory.CreateDirectory(Path.GetDirectoryName(packagedSource)!);
        CopyDirectory(Path.Combine(packagedWorkspace, "_estudio", "soporte", "vscode", "estudio-exercism"), packagedSource);
        var codeExe = Path.Combine(root, "Code.exe");
        var codeCmd = Path.Combine(root, "code.cmd");
        File.WriteAllText(codeExe, "");
        File.WriteAllText(codeCmd, "");
        var stalePath = Path.Combine(root, ".vscode", "extensions", "estudio-socratico.estudio-exercism-2.0.0");
        Directory.CreateDirectory(stalePath);
        File.WriteAllText(Path.Combine(stalePath, "stale.txt"), "old");
        var paths = new AppPaths(repoRoot: packagedRoot, localAppDataRoot: Path.Combine(root, "local"));
        var logManager = new LogManager(paths);
        var listCalls = 0;
        var runner = new RecordingRunner(spec =>
        {
            if (spec.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                spec.ArgumentString?.Contains("--list-extensions", StringComparison.OrdinalIgnoreCase) == true)
            {
                listCalls++;
                return RecordingRunner.Result(spec, 0, listCalls == 1
                    ? "estudio-socratico.estudio-exercism@2.0.0"
                    : "estudio-socratico.estudio-exercism@2.0.16");
            }

            return RecordingRunner.Result(spec, 0, "1.100.0");
        });
        var manager = new VSCodeManager(
            runner,
            new ExtensionManager(paths, logManager, userProfileRoot: root),
            new ManifestManager(paths),
            logManager,
            () => new VSCodePaths(codeExe, codeCmd));

        await manager.PrepareAsync(workspace, CancellationToken.None);

        var workspacePackage = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(workspace, "_estudio", "soporte", "vscode", "estudio-exercism", "package.json")))!.AsObject();
        Assert.Equal("2.0.16", workspacePackage["version"]?.GetValue<string>());
        Assert.Equal("assets/logo-vscode-extension.png", workspacePackage["icon"]?.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(workspace, "_estudio", "soporte", "vscode", "estudio-exercism", "assets", "estudio.svg")));
        Assert.True(Directory.Exists(Path.Combine(root, ".vscode", "extensions", "estudio-socratico.estudio-exercism-2.0.16")));
    }

    [Fact]
    public void VSCodeExtension_ActivityBarIconUsesNewBrandingAsset()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var extensionRoot = Path.Combine(repoRoot!, "_estudio", "soporte", "vscode", "estudio-exercism");
        var packageJson = JsonNode.Parse(File.ReadAllText(Path.Combine(extensionRoot, "package.json")))!.AsObject();
        var activityIconPath = Path.Combine(extensionRoot, "assets", "logo-vscode-activity.svg");
        var activityIconSvg = File.ReadAllText(activityIconPath);

        Assert.Equal("assets/logo-vscode-extension.png", packageJson["icon"]?.GetValue<string>());
        Assert.Equal(
            "assets/logo-vscode-activity.svg",
            packageJson["contributes"]?["viewsContainers"]?["activitybar"]?[0]?["icon"]?.GetValue<string>());
        Assert.Equal(
            "assets/logo-vscode-activity.svg",
            packageJson["contributes"]?["views"]?["estudioSocratico"]?[0]?["icon"]?.GetValue<string>());
        Assert.Contains("currentColor", activityIconSvg);
    }

    [Fact]
    public void FreshInstall_WritesVSCodeTaskForActiveCFile()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var tasksJson = JsonNode.Parse(File.ReadAllText(Path.Combine(repoRoot!, ".vscode", "tasks.json")))!.AsObject();
        var tasks = tasksJson["tasks"]!.AsArray();
        var buildTask = tasks.First(task => task?["label"]?.GetValue<string>() == "Compilar y Grabar (Sistema Socratico)")!.AsObject();

        Assert.Equal(".\\_estudio\\soporte\\scripts\\build.cmd", buildTask["command"]?.GetValue<string>());
        Assert.Contains(buildTask["args"]!.AsArray(), arg => arg?.GetValue<string>() == "${file}");
    }

    [Fact]
    public void FreshInstall_CtrlShiftBTaskUsesActiveCFile()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var tasksJson = JsonNode.Parse(File.ReadAllText(Path.Combine(repoRoot!, ".vscode", "tasks.json")))!.AsObject();
        var tasks = tasksJson["tasks"]!.AsArray();
        var buildTask = tasks.First(task => task?["label"]?.GetValue<string>() == "Compilar y Grabar (Sistema Socratico)")!.AsObject();

        Assert.True(buildTask["group"]?["isDefault"]?.GetValue<bool>());
        Assert.Equal("build", buildTask["group"]?["kind"]?.GetValue<string>());
        Assert.Contains(buildTask["args"]!.AsArray(), arg => arg?.GetValue<string>() == "${file}");
    }

    [Fact]
    public void FreshInstall_ConfiguresF9BindingOrExtensionCommand()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var extensionRoot = Path.Combine(repoRoot!, "_estudio", "soporte", "vscode", "estudio-exercism");
        var packageJson = JsonNode.Parse(File.ReadAllText(Path.Combine(extensionRoot, "package.json")))!.AsObject();
        var keybinding = packageJson["contributes"]?["keybindings"]?.AsArray()
            .FirstOrDefault(node => string.Equals(node?["key"]?.GetValue<string>(), "f9", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(keybinding);
        Assert.Equal("estudioExercism.compileActiveCFile", keybinding!["command"]?.GetValue<string>());
        Assert.Contains("resourceLangId == c", keybinding["when"]?.GetValue<string>());
    }

    [Fact]
    public void VSCodeExtension_ContributesF9CompileCommand()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var extensionRoot = Path.Combine(repoRoot!, "_estudio", "soporte", "vscode", "estudio-exercism");
        var packageJson = JsonNode.Parse(File.ReadAllText(Path.Combine(extensionRoot, "package.json")))!.AsObject();
        var extensionJs = File.ReadAllText(Path.Combine(extensionRoot, "extension.js"));
        var commands = packageJson["contributes"]?["commands"]?.AsArray();

        Assert.Contains(commands!, node => node?["command"]?.GetValue<string>() == "estudioExercism.compileActiveCFile");
        Assert.Contains("onCommand:estudioExercism.compileActiveCFile", packageJson["activationEvents"]!.ToJsonString());
        Assert.Contains("registerCommand(\"estudioExercism.compileActiveCFile\"", extensionJs);
        Assert.Contains("vscode.tasks.executeTask", extensionJs);
        Assert.Contains("Compilar y Grabar (Sistema Socratico)", extensionJs);
    }

    [Fact]
    public void VSCodeExtension_UsesCompactActivityBarInsteadOfEditorTitleText()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var extensionRoot = Path.Combine(repoRoot!, "_estudio", "soporte", "vscode", "estudio-exercism");
        var packageJson = JsonNode.Parse(File.ReadAllText(Path.Combine(extensionRoot, "package.json")))!.AsObject();

        Assert.Equal(
            "assets/logo-vscode-activity.svg",
            packageJson["contributes"]?["viewsContainers"]?["activitybar"]?[0]?["icon"]?.GetValue<string>());
        Assert.Null(packageJson["contributes"]?["menus"]?["editor/title"]);
        Assert.DoesNotContain("Estudio Socrático: Abrir Panel de Ejercicios", packageJson.ToJsonString());
    }

    [Fact]
    public void VSCodeExtension_PassesLocalByokConfigToManagerProcess()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var extensionRoot = Path.Combine(repoRoot!, "_estudio", "soporte", "vscode", "estudio-exercism");
        var extensionJs = File.ReadAllText(Path.Combine(extensionRoot, "extension.js"));

        Assert.Contains("model: \"gemini-2.5-flash\"", extensionJs);
        Assert.Contains("GEMINI_API_KEY", extensionJs);
        Assert.Contains("GEMINI_MODEL", extensionJs);
        Assert.Contains("ESTUDIO_EXTENSION_CONFIG_PATH", extensionJs);
        Assert.Contains("ESTUDIO_TRANSLATE_INTRODUCTIONS", extensionJs);
        Assert.Contains("getManagerEnvironment", extensionJs);
        Assert.Contains("env: options.env || getManagerEnvironment(root)", extensionJs);
    }

    private static VSCodeManager CreateManager(string root, ICommandRunner runner, Func<VSCodePaths> locator)
    {
        var paths = new AppPaths(localAppDataRoot: Path.Combine(root, "local"));
        var logManager = new LogManager(paths);
        return new VSCodeManager(runner, new ExtensionManager(paths, logManager, userProfileRoot: root), new ManifestManager(paths), logManager, locator);
    }

    private static string CreateWorkspaceWithExtension(string root, string version = "1.0.0", bool useLegacyBranding = false)
    {
        var workspace = Path.Combine(root, "workspace");
        var extension = Path.Combine(workspace, "_estudio", "soporte", "vscode", "estudio-exercism");
        Directory.CreateDirectory(extension);
        Directory.CreateDirectory(Path.Combine(extension, "assets"));
        Directory.CreateDirectory(Path.Combine(workspace, "_estudio", "soporte", "exercism"));
        var packageJson = useLegacyBranding
            ? $"{{\"name\":\"estudio-exercism\",\"publisher\":\"estudio-socratico\",\"version\":\"{version}\",\"icon\":\"assets/estudio.png\",\"contributes\":{{\"viewsContainers\":{{\"activitybar\":[{{\"id\":\"estudioSocratico\",\"title\":\"Estudio\",\"icon\":\"assets/estudio.svg\"}}]}},\"views\":{{\"estudioSocratico\":[{{\"id\":\"estudioExercism.view\",\"name\":\"Ejercicios\",\"icon\":\"assets/estudio.svg\"}}]}},\"commands\":[{{\"command\":\"estudioExercism.compileActiveCFile\"}},{{\"command\":\"estudioExercism.openPanel\"}},{{\"command\":\"estudioExercism.openApiKeyConfig\"}},{{\"command\":\"estudioExercism.revealApiKeyConfig\"}}]}}}}"
            : $"{{\"name\":\"estudio-exercism\",\"publisher\":\"estudio-socratico\",\"version\":\"{version}\",\"icon\":\"assets/logo-vscode-extension.png\",\"contributes\":{{\"viewsContainers\":{{\"activitybar\":[{{\"id\":\"estudioSocratico\",\"title\":\"Estudio\",\"icon\":\"assets/logo-vscode-activity.svg\"}}]}},\"views\":{{\"estudioSocratico\":[{{\"id\":\"estudioExercism.view\",\"name\":\"Ejercicios\",\"icon\":\"assets/logo-vscode-activity.svg\"}}]}},\"commands\":[{{\"command\":\"estudioExercism.compileActiveCFile\"}},{{\"command\":\"estudioExercism.openPanel\"}},{{\"command\":\"estudioExercism.openApiKeyConfig\"}},{{\"command\":\"estudioExercism.revealApiKeyConfig\"}}]}}}}";
        File.WriteAllText(Path.Combine(extension, "package.json"), packageJson);
        if (useLegacyBranding)
        {
            File.WriteAllBytes(Path.Combine(extension, "assets", "estudio.png"), [0x89, 0x50, 0x4E, 0x47]);
            File.WriteAllText(Path.Combine(extension, "assets", "estudio.svg"), """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="#ffffff" d="M12 2 4 6v12l8 4 8-4V6z"/></svg>""");
        }
        else
        {
            File.WriteAllBytes(Path.Combine(extension, "assets", "logo-vscode-extension.png"), [0x89, 0x50, 0x4E, 0x47]);
            File.WriteAllText(Path.Combine(extension, "assets", "logo-vscode-activity.svg"), """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="currentColor" d="M12 2 4 6v12l8 4 8-4V6z"/></svg>""");
        }
        File.WriteAllText(Path.Combine(workspace, "_estudio", "soporte", "exercism", "manager.ps1"), "");
        return workspace;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal), overwrite: true);
        }
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
