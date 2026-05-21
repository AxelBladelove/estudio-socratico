using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class WorkflowBackendTests
{
    [Fact]
    public void Exercism_C_Track_Url_Is_Official_Target()
    {
        Assert.Equal("https://exercism.org/tracks/c", ExercismManager.CTrackUrl);
    }

    [Fact]
    public async Task ConfigureExercism_Redacts_Token_In_Command_Logs()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var logManager = new LogManager(paths);
        var token = "abcdefghijklmnopqrstuvwxyz123456";
        var runner = new LoggingSuccessfulRunner(logManager);
        var manager = new ExercismManager(runner, new ManifestManager(paths), paths, logManager);

        await manager.ConfigureTokenAsync(token, workspace, CancellationToken.None);

        var log = await File.ReadAllTextAsync(logManager.InstallerLogPath);
        Assert.DoesNotContain(token, log);
        Assert.Contains("[REDACTED]", log);
    }

    [Fact]
    public async Task ConfigureExercism_Redacts_Token_And_Uuid_From_Output()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var logManager = new LogManager(paths);
        var token = "abcdefghijklmnopqrstuvwxyz123456";
        var uuid = "123e4567-e89b-12d3-a456-426614174000";
        var runner = new LoggingRunner(logManager, spec => new CommandResult
        {
            Spec = spec,
            ExitCode = 0,
            StandardOutput = $"Token {token}\nrequest id {uuid}",
            Duration = TimeSpan.Zero
        });
        var manager = new ExercismManager(runner, new ManifestManager(paths), paths, logManager);

        await manager.ConfigureTokenAsync(token, workspace, CancellationToken.None);

        var log = await File.ReadAllTextAsync(logManager.InstallerLogPath);
        Assert.DoesNotContain(token, log);
        Assert.DoesNotContain(uuid, log);
        Assert.Contains("[REDACTED]", log);
    }

    [Fact]
    public async Task ExercismDetector_Uses_Help_Instead_Of_Version()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var exercism = Path.Combine(root, "exercism.exe");
        File.WriteAllText(exercism, "");
        var runner = new RecordingRunner(spec => spec.FileName == "where.exe"
            ? RecordingRunner.Result(spec, 0, exercism + Environment.NewLine)
            : RecordingRunner.Result(spec, 0, "Exercism CLI"));
        var detector = new DependencyDetector(runner);

        _ = await detector.DetectAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.ExercismCli));

        Assert.DoesNotContain("--version", runner.Specs.SelectMany(x => x.Arguments));
        Assert.Contains(runner.Specs, x => x.Arguments.Contains("help"));
    }

    [Fact]
    public async Task ExercismValidateToken_Does_Not_Use_OutputDir()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var runner = new RecordingSuccessfulRunner();
        var manager = new ExercismManager(runner, new ManifestManager(paths), paths, new LogManager(paths));

        await manager.ValidateTokenAsync(CancellationToken.None);

        var download = Assert.Single(runner.Specs, spec => spec.Arguments.Contains("download"));
        Assert.DoesNotContain("--output-dir", download.Arguments);
        Assert.Equal(Path.Combine(paths.LocalAppDataRoot, "Diagnostics", "exercism-token-check"), download.WorkingDirectory);
    }

    [Fact]
    public async Task ExercismValidation_ExistingHelloWorld_DoesNotFailToken()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var runner = new RecordingRunner(spec =>
        {
            if (spec.Arguments.Contains("version"))
                return RecordingRunner.Result(spec, 0, "3.0.0");
            if (spec.Arguments.Contains("workspace"))
                return RecordingRunner.Result(spec, 0, workspace);
            if (spec.Arguments.Contains("configure"))
                return RecordingRunner.Result(spec, 0);
            if (spec.Arguments.Contains("download"))
                return RecordingRunner.Result(spec, 1, "", "Error: directory '/some/path/c/hello-world' already exists");
            return RecordingRunner.Result(spec, 0);
        });
        var manager = new ExercismManager(runner, new ManifestManager(paths), paths, new LogManager(paths));

        // Call ValidateTokenAsync -> should NOT throw
        await manager.ValidateTokenAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExercismValidation_InvalidToken_FailsWithAuthMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var runner = new RecordingRunner(spec =>
        {
            if (spec.Arguments.Contains("version"))
                return RecordingRunner.Result(spec, 0, "3.0.0");
            if (spec.Arguments.Contains("workspace"))
                return RecordingRunner.Result(spec, 0, workspace);
            if (spec.Arguments.Contains("configure"))
                return RecordingRunner.Result(spec, 0);
            if (spec.Arguments.Contains("download"))
                return RecordingRunner.Result(spec, 1, "", "Error: unauthorized: invalid token");
            return RecordingRunner.Result(spec, 0);
        });
        var manager = new ExercismManager(runner, new ManifestManager(paths), paths, new LogManager(paths));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ValidateTokenAsync(CancellationToken.None));
        Assert.Contains("no autorizado", ex.Message);
    }

    [Fact]
    public async Task ExercismValidation_DoesNotOverwriteStudentWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var runner = new RecordingRunner(spec =>
        {
            if (spec.Arguments.Contains("version"))
                return RecordingRunner.Result(spec, 0, "3.0.0");
            if (spec.Arguments.Contains("workspace"))
                return RecordingRunner.Result(spec, 0, workspace);
            if (spec.Arguments.Contains("configure"))
                return RecordingRunner.Result(spec, 0);
            if (spec.Arguments.Contains("download"))
                return RecordingRunner.Result(spec, 0);
            return RecordingRunner.Result(spec, 0);
        });
        var manager = new ExercismManager(runner, new ManifestManager(paths), paths, new LogManager(paths));

        await manager.ValidateTokenAsync(CancellationToken.None);

        var downloadSpec = Assert.Single(runner.Specs, spec => spec.Arguments.Contains("download"));
        Assert.Contains("--force", downloadSpec.Arguments);
    }

    [Fact]
    public async Task ExercismValidation_UsesTempDirectoryForSmokeDownload()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var runner = new RecordingRunner(spec =>
        {
            if (spec.Arguments.Contains("version"))
                return RecordingRunner.Result(spec, 0, "3.0.0");
            if (spec.Arguments.Contains("workspace"))
                return RecordingRunner.Result(spec, 0, workspace);
            if (spec.Arguments.Contains("configure"))
                return RecordingRunner.Result(spec, 0);
            if (spec.Arguments.Contains("download"))
                return RecordingRunner.Result(spec, 0);
            return RecordingRunner.Result(spec, 0);
        });
        var manager = new ExercismManager(runner, new ManifestManager(paths), paths, new LogManager(paths));

        await manager.ValidateTokenAsync(CancellationToken.None);

        var downloadSpec = Assert.Single(runner.Specs, spec => spec.Arguments.Contains("download"));
        Assert.Contains("exercism-token-check", downloadSpec.WorkingDirectory);
    }

    [Fact]
    public async Task ExercismValidation_DistinguishesExistingFromInvalidToken()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(root, "LocalAppData"));

        // Case A: directory already exists -> Success
        var runnerA = new RecordingRunner(spec =>
        {
            if (spec.Arguments.Contains("version"))
                return RecordingRunner.Result(spec, 0, "3.0.0");
            if (spec.Arguments.Contains("workspace"))
                return RecordingRunner.Result(spec, 0, workspace);
            if (spec.Arguments.Contains("configure"))
                return RecordingRunner.Result(spec, 0);
            if (spec.Arguments.Contains("download"))
                return RecordingRunner.Result(spec, 1, "", "Error: directory already exists");
            return RecordingRunner.Result(spec, 0);
        });
        var managerA = new ExercismManager(runnerA, new ManifestManager(paths), paths, new LogManager(paths));
        await managerA.ValidateTokenAsync(CancellationToken.None);

        // Case B: unauthorized -> Throws
        var runnerB = new RecordingRunner(spec =>
        {
            if (spec.Arguments.Contains("version"))
                return RecordingRunner.Result(spec, 0, "3.0.0");
            if (spec.Arguments.Contains("workspace"))
                return RecordingRunner.Result(spec, 0, workspace);
            if (spec.Arguments.Contains("configure"))
                return RecordingRunner.Result(spec, 0);
            if (spec.Arguments.Contains("download"))
                return RecordingRunner.Result(spec, 1, "", "Error: unauthorized: bad token");
            return RecordingRunner.Result(spec, 0);
        });
        var managerB = new ExercismManager(runnerB, new ManifestManager(paths), paths, new LogManager(paths));
        await Assert.ThrowsAsync<InvalidOperationException>(() => managerB.ValidateTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ExercismDetector_Prefers_Managed_Tools_Path()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-managed-" + Guid.NewGuid().ToString("N"));
        var tools = Path.Combine(root, "Tools", "bin");
        Directory.CreateDirectory(tools);
        var exercism = Path.Combine(tools, "exercism.exe");
        File.WriteAllText(exercism, "");
        var runner = new RecordingRunner(spec => spec.FileName == "where.exe"
            ? RecordingRunner.Result(spec, 1)
            : RecordingRunner.Result(spec, 0, "A command-line interface for Exercism."));
        var detector = new DependencyDetector(runner, managedToolsDirectory: tools);

        var state = await detector.DetectAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.ExercismCli));

        Assert.Equal(DependencyStatus.Ready, state.Status);
        Assert.Equal(exercism, state.Path);
    }

    [Fact]
    public async Task GitHub_Does_Not_Fork_When_Active_User_Owns_Upstream()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-github-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var manifest = new ManifestManager(new AppPaths(repoRoot: root, localAppDataRoot: Path.Combine(root, "local")));
        var runner = new RecordingRunner(spec =>
        {
            if (spec.Arguments.SequenceEqual(["api", "user", "--jq", ".login"]))
            {
                return RecordingRunner.Result(spec, 0, "AxelBladelove\n");
            }

            return RecordingRunner.Result(spec, 0, "ok");
        });
        var manager = new GitHubAccountManager(runner, manifest, new LogManager(new AppPaths(repoRoot: root, localAppDataRoot: Path.Combine(root, "local"))));

        await manager.ConfigureRepositoryAsync(root, "alejandro", CancellationToken.None);

        Assert.DoesNotContain(runner.Specs, spec => spec.Arguments.Take(2).SequenceEqual(["repo", "fork"]));
        Assert.DoesNotContain(runner.Specs, spec => spec.Arguments.Contains("--remote=false"));
    }

    [Fact]
    public async Task ConfigureExercism_DoesNotCreate_TargetWorkspace_Before_Repo_Exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-exercism-preconfig-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "Estudio-Socratico-erick");
        var paths = new AppPaths(repoRoot: root, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var runner = new RecordingRunner(spec =>
        {
            if (spec.Arguments.Contains("version"))
                return RecordingRunner.Result(spec, 0, "3.0.0");
            if (spec.Arguments.Count == 1 && spec.Arguments[0] == "workspace")
                return RecordingRunner.Result(spec, 0, Path.Combine(paths.LocalAppDataRoot, "Diagnostics", "preconfigured-exercism", "usuario", "exercism"));
            if (spec.Arguments.Contains("configure") || spec.Arguments.Contains("download"))
                return RecordingRunner.Result(spec, 0);
            return RecordingRunner.Result(spec, 0, "ok");
        });
        var engine = new ConfiguratorEngine(paths, runner);

        await engine.ConfigureExercismAsync("abcdefghijklmnopqrstuvwxyz123456", workspace);

        Assert.False(Directory.Exists(workspace));
        var configure = Assert.Single(runner.Specs, spec => spec.Arguments.Contains("--token"));
        Assert.Contains(Path.Combine(paths.LocalAppDataRoot, "Diagnostics", "preconfigured-exercism", "usuario", "exercism"), configure.Arguments);
    }

    [Fact]
    public async Task BootstrapWorkspace_Is_Adopted_Without_Losing_User_Data()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-bootstrap-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "Estudio-Socratico-erick");
        Directory.CreateDirectory(Path.Combine(target, "usuario", "exercism"));
        Directory.CreateDirectory(Path.Combine(target, "usuario", "config"));
        await File.WriteAllTextAsync(Path.Combine(target, "usuario", "config", "estudio-socratico.extension.local.json"), "{ }");
        await File.WriteAllTextAsync(Path.Combine(target, ".estudio_usuario"), "erick");
        await File.WriteAllTextAsync(Path.Combine(target, ".gitignore"), "usuario/config/estudio-socratico.extension.local.json\n");

        var paths = new AppPaths(repoRoot: root, localAppDataRoot: Path.Combine(root, "LocalAppData"));
        var manifest = new ManifestManager(paths);
        var runner = new RecordingRunner(spec =>
        {
            if (spec.FileName == "git" && spec.Arguments.Count >= 3 && spec.Arguments[0] == "clone")
            {
                var cloneTarget = spec.Arguments[^1];
                WriteMinimalClonedWorkspace(cloneTarget);
                return RecordingRunner.Result(spec, 0, "cloned");
            }

            return RecordingRunner.Result(spec, 0, "ok");
        });
        var manager = new GitHubAccountManager(runner, manifest, new LogManager(paths));

        var actual = await manager.EnsureWorkspaceRepositoryAsync(target, "erick", skipGitHub: true, CancellationToken.None);

        Assert.Equal(target, actual);
        Assert.True(File.Exists(Path.Combine(target, "AGENTS.md")));
        Assert.True(File.Exists(Path.Combine(target, "usuario", "config", "estudio-socratico.extension.local.json")));
        Assert.Equal("erick", await File.ReadAllTextAsync(Path.Combine(target, ".estudio_usuario")));
    }

    [Fact]
    public async Task SmokeTest_Uses_F9_Build_Flow_Without_Automatic_Commits()
    {
        var workspace = CreateSmokeWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(workspace, "LocalAppData"));
        var runner = new RecordingSuccessfulRunner();
        var engine = new ConfiguratorEngine(paths, runner);

        var summary = await engine.RunSmokeTestAsync(workspace);

        var build = Assert.Single(runner.Specs, spec => spec.FileName.EndsWith("build.cmd", StringComparison.OrdinalIgnoreCase));
        Assert.True(summary.Succeeded);
        Assert.True(build.Environment.TryGetValue("ESTUDIO_SKIP_COMMIT", out var skipCommit));
        Assert.Equal("1", skipCommit);
    }

    [Fact]
    public async Task SmokeTest_NonInteractive_DoesNotWaitForKey()
    {
        var workspace = CreateSmokeWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(workspace, "LocalAppData"));
        var runner = new RecordingSuccessfulRunner();
        var engine = new ConfiguratorEngine(paths, runner);

        _ = await engine.RunSmokeTestAsync(workspace);

        var build = Assert.Single(runner.Specs, spec => spec.FileName.EndsWith("build.cmd", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("--installer-smoke", build.Arguments);
        Assert.Contains("--non-interactive", build.Arguments);
    }

    [Fact]
    public async Task SmokeTest_ProbeOutputsOk_ReturnsSuccess()
    {
        var workspace = CreateSmokeWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(workspace, "LocalAppData"));
        var runner = new RecordingSuccessfulRunner();
        var engine = new ConfiguratorEngine(paths, runner);

        var summary = await engine.RunSmokeTestAsync(workspace);

        Assert.True(summary.Succeeded);
        Assert.Equal("passed", summary.CurrentState!.FinalReadiness.SmokeTestStatus);
    }

    [Fact]
    public async Task SmokeTest_SetsSkipCommitAndSkipPause()
    {
        var workspace = CreateSmokeWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(workspace, "LocalAppData"));
        var runner = new RecordingSuccessfulRunner();
        var engine = new ConfiguratorEngine(paths, runner);

        _ = await engine.RunSmokeTestAsync(workspace);

        var build = Assert.Single(runner.Specs, spec => spec.FileName.EndsWith("build.cmd", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("1", build.Environment["ESTUDIO_SKIP_COMMIT"]);
        Assert.Equal("1", build.Environment["ESTUDIO_SKIP_PAUSE"]);
        Assert.Equal("1", build.Environment["ESTUDIO_NONINTERACTIVE"]);
        Assert.Equal("1", build.Environment["ESTUDIO_INSTALLER_SMOKE"]);
    }

    [Fact]
    public void SmokeTest_InteractiveTailDoesNotCauseFalseFailure()
    {
        var result = new CommandResult
        {
            Spec = new CommandSpec { FileName = "build.cmd" },
            ExitCode = -1,
            TimedOut = true,
            StandardOutput = "[OK] Compilacion exitosa -> Ejecutando probe.exe en esta terminal...\nok\nProcess returned 0 (0x0)\nPress any key to continue.",
            StandardError = "Command timed out."
        };

        Assert.True(TelemetryCompatibilityManager.IsInteractiveTailSuccess(result));
    }

    [Fact]
    public async Task BuildCmd_InstallerSmokeMode_ExitsZero()
    {
        var workspace = CreateRealScriptWorkspace();
        var buildScript = Path.Combine(workspace, "_estudio", "soporte", "scripts", "build.cmd");
        var probe = Path.Combine(workspace, "_estudio", "soporte", "runtime", "installer-probe", "probe.c");
        Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
        await File.WriteAllTextAsync(probe, "#include <stdio.h>\nint main(void){printf(\"ok\\n\");return 0;}\n");

        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = buildScript,
            WorkingDirectory = workspace,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("--inline");
        process.StartInfo.ArgumentList.Add(probe);
        process.StartInfo.ArgumentList.Add("--installer-smoke");
        process.StartInfo.ArgumentList.Add("--non-interactive");
        process.StartInfo.Environment["ESTUDIO_INSTALLER_SMOKE"] = "1";
        process.StartInfo.Environment["ESTUDIO_NONINTERACTIVE"] = "1";
        process.StartInfo.Environment["ESTUDIO_SKIP_PAUSE"] = "1";
        process.StartInfo.Environment["ESTUDIO_SKIP_COMMIT"] = "1";

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("[OK] Compilacion exitosa", stdout);
        Assert.Contains("ok", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Process returned 0", stdout);
        Assert.DoesNotContain("Press any key to continue", stdout);
        Assert.DoesNotContain("Press Enter to continue", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);
    }

    private static string CreateSmokeWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "scripts"));
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# test");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "build.cmd"), "@echo off");
        return root;
    }

    private static void WriteMinimalClonedWorkspace(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "include"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "exercism"));
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# cloned");
        File.WriteAllText(Path.Combine(root, ".gitignore"), "bin/\n");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "build.cmd"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "compilar_y_grabar.bat"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "include", "conio.h"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "exercism", "manager.ps1"), "");
    }

    private static string CreateRealScriptWorkspace()
    {
        var repoRoot = FindRepoRoot();
        var root = Path.Combine(Path.GetTempPath(), "estudio-smoke-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.Copy(Path.Combine(repoRoot, "AGENTS.md"), Path.Combine(root, "AGENTS.md"));

        CopyFile(repoRoot, root, Path.Combine("_estudio", "soporte", "scripts", "build.cmd"));
        CopyFile(repoRoot, root, Path.Combine("_estudio", "soporte", "scripts", "compilar_y_grabar.bat"));
        CopyFile(repoRoot, root, Path.Combine("_estudio", "soporte", "scripts", "resolve_build_context.ps1"));
        CopyFile(repoRoot, root, Path.Combine("_estudio", "soporte", "scripts", "finalizar_intento.bat"));
        CopyFile(repoRoot, root, Path.Combine("_estudio", "soporte", "consola", "output_launcher.c"));
        CopyFile(repoRoot, root, Path.Combine("_estudio", "soporte", "consola", "conio.c"));
        CopyFile(repoRoot, root, Path.Combine("_estudio", "include", "conio.h"));
        CopyFile(repoRoot, root, Path.Combine("_estudio", "include", "estudio_stdio_cp437.h"));

        return root;
    }

    private static void CopyFile(string repoRoot, string destinationRoot, string relativePath)
    {
        var source = Path.Combine(repoRoot, relativePath);
        var destination = Path.Combine(destinationRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static string FindRepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "AGENTS.md")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("No se pudo localizar la raiz del repositorio para la prueba real de build.cmd.");
    }

    private sealed class RecordingSuccessfulRunner : ICommandRunner
    {
        public List<CommandSpec> Specs { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            Specs.Add(spec);
            return Task.FromResult(new CommandResult
            {
                Spec = spec,
                ExitCode = 0,
                StandardOutput = "ok",
                Duration = TimeSpan.Zero
            });
        }
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

    private sealed class LoggingRunner(LogManager logManager, Func<CommandSpec, CommandResult> handler) : ICommandRunner
    {
        public async Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            await logManager.WriteCommandAsync(spec, null, cancellationToken).ConfigureAwait(false);
            var result = handler(spec);
            await logManager.WriteCommandAsync(spec, result, cancellationToken).ConfigureAwait(false);
            return result;
        }
    }

    private sealed class LoggingSuccessfulRunner(LogManager logManager) : ICommandRunner
    {
        public async Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            await logManager.WriteCommandAsync(spec, null, cancellationToken).ConfigureAwait(false);
            var result = new CommandResult
            {
                Spec = spec,
                ExitCode = 0,
                StandardOutput = "ok",
                Duration = TimeSpan.Zero
            };
            await logManager.WriteCommandAsync(spec, result, cancellationToken).ConfigureAwait(false);
            return result;
        }
    }
}
