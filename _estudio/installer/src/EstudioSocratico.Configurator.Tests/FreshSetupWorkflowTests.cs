using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class FreshSetupWorkflowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _localAppData;
    private readonly AppPaths _paths;
    private readonly string _nodeExe;
    private readonly string _pythonExe;
    private readonly string _gitExe;
    private readonly string _ghExe;
    private readonly string _exercismExe;
    private readonly string _wingetExe;
    private readonly string _codeExe;
    private readonly string _codeCmd;
    private readonly string _pythonAliasExe;

    public FreshSetupWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"estudio-fresh-setup-{Guid.NewGuid():N}");
        _localAppData = Path.Combine(_tempDir, "LocalAppData");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_localAppData);
        _paths = new AppPaths(repoRoot: _tempDir, localAppDataRoot: _localAppData);

        _nodeExe = Path.Combine(_tempDir, "node.exe");
        _pythonExe = Path.Combine(_tempDir, "python-real.exe");
        _gitExe = Path.Combine(_tempDir, "git.exe");
        _ghExe = Path.Combine(_tempDir, "gh.exe");
        _exercismExe = Path.Combine(_tempDir, "exercism.exe");
        _wingetExe = Path.Combine(_tempDir, "winget.exe");
        _codeExe = Path.Combine(_tempDir, "Code.exe");
        _codeCmd = Path.Combine(_tempDir, "code.cmd");
        _pythonAliasExe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "python.exe");

        File.WriteAllText(_codeExe, string.Empty);
        File.WriteAllText(_codeCmd, string.Empty);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempDir))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temp test files.
        }
    }

    [Fact]
    public async Task FreshSetup_MissingTools_InstallsBeforeAccounts()
    {
        var runner = new FreshSetupRunner(
            _nodeExe,
            _pythonExe,
            _gitExe,
            _ghExe,
            _exercismExe,
            _wingetExe,
            _codeExe,
            _codeCmd,
            _pythonAliasExe);
        var engine = CreateEngine(runner);

        var summary = await engine.RunAsync(new SetupRequest
        {
            Mode = SetupMode.Install,
            LocalAlias = "estudiante",
            WorkspacePath = "Estudio-Socratico-estudiante",
            SkipGitHubLogin = true,
            SkipExercism = true
        });

        Assert.Empty(summary.Errors);
        Assert.Contains(runner.Specs, spec => IsWingetInstall(spec, "OpenJS.NodeJS.LTS"));
        Assert.Contains(runner.Specs, spec => IsWingetInstall(spec, "Python.Python.3.13"));
        Assert.Contains(runner.Specs, spec => IsWingetInstall(spec, "Git.Git"));
        Assert.Contains(runner.Specs, spec => IsWingetInstall(spec, "GitHub.cli"));
        Assert.Contains(runner.Specs, spec => IsWingetInstall(spec, "Exercism.CLI"));
        Assert.Contains(runner.Specs, spec => string.Equals(spec.FileName, Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "bash.exe"), StringComparison.OrdinalIgnoreCase)
            && spec.Arguments.Contains("-lc"));
        Assert.DoesNotContain(runner.Specs, spec => spec.Arguments.Contains("clone"));
        Assert.DoesNotContain(runner.Specs, spec => spec.Arguments.Contains("auth"));
        Assert.Equal(GlobalState.NeedsAuthentication, summary.CurrentState!.GlobalState);
    }

    [Fact]
    public async Task FreshSetup_WorkspacePath_IsAbsoluteUnderUserProfile()
    {
        var runner = new FreshSetupRunner(
            _nodeExe,
            _pythonExe,
            _gitExe,
            _ghExe,
            _exercismExe,
            _wingetExe,
            _codeExe,
            _codeCmd,
            _pythonAliasExe);
        var engine = CreateEngine(runner);

        var summary = await engine.RunAsync(new SetupRequest
        {
            Mode = SetupMode.Install,
            LocalAlias = "estudiante",
            WorkspacePath = "Estudio-Socratico-estudiante",
            SkipGitHubLogin = true,
            SkipExercism = true
        });
        var manifest = await new ManifestManager(_paths).LoadAsync();
        var expected = Path.Combine(_paths.UserProfileRoot, "Estudio-Socratico-estudiante");

        Assert.Equal(expected, summary.WorkspacePath);
        Assert.Equal(expected, manifest.WorkspacePath);
        Assert.Equal(expected, summary.CurrentState!.WorkspacePath);
        Assert.Equal(expected, summary.CurrentState.RecommendedWorkspacePath);
        Assert.Equal(expected, summary.CurrentState.WorkspaceContext.WorkspacePath);
        Assert.Equal(expected, summary.CurrentState.FinalReadiness.WorkspacePath);
        Assert.False(manifest.BuildFlowValidated);
    }

    [Fact]
    public async Task FreshSetup_RevalidatesDependenciesAfterInstall()
    {
        var runner = new FreshSetupRunner(
            _nodeExe,
            _pythonExe,
            _gitExe,
            _ghExe,
            _exercismExe,
            _wingetExe,
            _codeExe,
            _codeCmd,
            _pythonAliasExe);
        var engine = CreateEngine(runner);

        var summary = await engine.RunAsync(new SetupRequest
        {
            Mode = SetupMode.Install,
            LocalAlias = "estudiante",
            WorkspacePath = "Estudio-Socratico-estudiante",
            SkipGitHubLogin = true,
            SkipExercism = true
        });

        foreach (var dependencyId in new[]
                 {
                     DependencyId.NodeJs,
                     DependencyId.Python,
                     DependencyId.Git,
                     DependencyId.GitHubCli,
                     DependencyId.ExercismCli,
                     DependencyId.Msys2,
                     DependencyId.Gcc,
                     DependencyId.Make
                 })
        {
            Assert.Equal(
                DependencyStatus.Ready,
                summary.Dependencies.Single(dep => dep.Id == dependencyId).Status);
        }

        Assert.True(runner.WhereCalls.GetValueOrDefault("gh") >= 2);
        Assert.True(runner.WhereCalls.GetValueOrDefault("python") >= 2);
        Assert.True(runner.WhereCalls.GetValueOrDefault("git") >= 2);
    }

    private ConfiguratorEngine CreateEngine(FreshSetupRunner runner)
    {
        return new ConfiguratorEngine(
            _paths,
            runner,
            runner.FileExists,
            () => runner.VsCodeInstalled ? new VSCodePaths(_codeExe, _codeCmd) : new VSCodePaths(null, null));
    }

    private static bool IsWingetInstall(CommandSpec spec, string packageId)
    {
        var isWinget = string.Equals(spec.FileName, "winget", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(Path.GetFileName(spec.FileName), "winget.exe", StringComparison.OrdinalIgnoreCase);
        return isWinget && spec.Arguments.Contains(packageId);
    }

    private sealed class FreshSetupRunner(
        string nodeExe,
        string pythonExe,
        string gitExe,
        string ghExe,
        string exercismExe,
        string wingetExe,
        string codeExe,
        string codeCmd,
        string pythonAliasExe) : ICommandRunner
    {
        public List<CommandSpec> Specs { get; } = [];
        public Dictionary<string, int> WhereCalls { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool NodeInstalled { get; private set; }
        public bool PythonInstalled { get; private set; }
        public bool GitInstalled { get; private set; }
        public bool GhInstalled { get; private set; }
        public bool ExercismInstalled { get; private set; }
        public bool VsCodeInstalled { get; private set; }
        public bool Msys2Installed { get; private set; }
        public bool GccInstalled { get; private set; }
        public bool MakeInstalled { get; private set; }

        public Func<string, bool> FileExists => path =>
        {
            if (string.Equals(path, pythonAliasExe, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(path, nodeExe, StringComparison.OrdinalIgnoreCase))
            {
                return NodeInstalled;
            }

            if (string.Equals(path, pythonExe, StringComparison.OrdinalIgnoreCase))
            {
                return PythonInstalled;
            }

            if (string.Equals(path, gitExe, StringComparison.OrdinalIgnoreCase))
            {
                return GitInstalled;
            }

            if (string.Equals(path, ghExe, StringComparison.OrdinalIgnoreCase))
            {
                return GhInstalled;
            }

            if (string.Equals(path, exercismExe, StringComparison.OrdinalIgnoreCase))
            {
                return ExercismInstalled;
            }

            if (string.Equals(path, wingetExe, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(path, ProductInfo.DefaultMsys2Root, StringComparison.OrdinalIgnoreCase))
            {
                return Msys2Installed;
            }

            if (string.Equals(path, Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "bash.exe"), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "pacman.exe"), StringComparison.OrdinalIgnoreCase))
            {
                return Msys2Installed;
            }

            if (string.Equals(path, Path.Combine(ProductInfo.DefaultMsys2UcrtBin, "gcc.exe"), StringComparison.OrdinalIgnoreCase))
            {
                return GccInstalled;
            }

            if (string.Equals(path, Path.Combine(ProductInfo.DefaultMsys2UcrtBin, "make.exe"), StringComparison.OrdinalIgnoreCase))
            {
                return MakeInstalled;
            }

            if (string.Equals(path, codeExe, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, codeCmd, StringComparison.OrdinalIgnoreCase))
            {
                return VsCodeInstalled;
            }

            return false;
        };

        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            Specs.Add(spec);
            var fileName = Path.GetFileName(spec.FileName).ToLowerInvariant();

            if (fileName == "where.exe")
            {
                var query = spec.Arguments.FirstOrDefault() ?? string.Empty;
                WhereCalls[query] = WhereCalls.TryGetValue(query, out var count) ? count + 1 : 1;
                return Task.FromResult(query switch
                {
                    "node" when NodeInstalled => Result(spec, 0, nodeExe + Environment.NewLine),
                    "python" when PythonInstalled => Result(spec, 0, pythonAliasExe + Environment.NewLine + pythonExe + Environment.NewLine),
                    "python" => Result(spec, 0, pythonAliasExe + Environment.NewLine),
                    "git" when GitInstalled => Result(spec, 0, gitExe + Environment.NewLine),
                    "gh" when GhInstalled => Result(spec, 0, ghExe + Environment.NewLine),
                    "exercism" when ExercismInstalled => Result(spec, 0, exercismExe + Environment.NewLine),
                    "winget" => Result(spec, 0, wingetExe + Environment.NewLine),
                    _ => Result(spec, 1, error: "INFO: Could not find files for the given pattern(s).")
                });
            }

            if (fileName is "winget" or "winget.exe")
            {
                if (spec.Arguments.Contains("--info"))
                {
                    return Task.FromResult(Result(spec, 0, "Windows Package Manager v1.8.1911"));
                }

                if (spec.Arguments.SequenceEqual(["source", "list"]))
                {
                    return Task.FromResult(Result(spec, 0, "winget https://cdn.winget.microsoft.com/cache"));
                }

                if (spec.Arguments.Contains("install"))
                {
                    if (spec.Arguments.Contains("OpenJS.NodeJS.LTS")) NodeInstalled = true;
                    if (spec.Arguments.Contains("Python.Python.3.13")) PythonInstalled = true;
                    if (spec.Arguments.Contains("Git.Git")) GitInstalled = true;
                    if (spec.Arguments.Contains("GitHub.cli")) GhInstalled = true;
                    if (spec.Arguments.Contains("Exercism.CLI")) ExercismInstalled = true;
                    if (spec.Arguments.Contains("Microsoft.VisualStudioCode")) VsCodeInstalled = true;
                    if (spec.Arguments.Contains("MSYS2.MSYS2")) Msys2Installed = true;
                    return Task.FromResult(Result(spec, 0, "installed"));
                }
            }

            if (string.Equals(spec.FileName, pythonAliasExe, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result(
                    spec,
                    9009,
                    error: "Python was not found; run without arguments to install from the Microsoft Store, or disable this shortcut from Settings > Apps > Advanced app settings > App execution aliases."));
            }

            if (Matches(spec.FileName, nodeExe, "node"))
            {
                return Task.FromResult(NodeInstalled
                    ? Result(spec, 0, "v22.16.0")
                    : Result(spec, 1, error: "node missing"));
            }

            if (Matches(spec.FileName, pythonExe, "python"))
            {
                return Task.FromResult(PythonInstalled
                    ? Result(spec, 0, "Python 3.13.1")
                    : Result(spec, 1, error: "python missing"));
            }

            if (Matches(spec.FileName, gitExe, "git"))
            {
                return Task.FromResult(GitInstalled
                    ? Result(spec, 0, "git version 2.45.1.windows.1")
                    : Result(spec, 1, error: "git missing"));
            }

            if (Matches(spec.FileName, ghExe, "gh"))
            {
                return Task.FromResult(GhInstalled
                    ? Result(spec, 0, "gh version 2.72.0")
                    : Result(spec, 1, error: "gh missing"));
            }

            if (Matches(spec.FileName, exercismExe, "exercism"))
            {
                return Task.FromResult(ExercismInstalled
                    ? Result(spec, 0, "Exercism CLI 3.5.4")
                    : Result(spec, 1, error: "exercism missing"));
            }

            if (string.Equals(spec.FileName, Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "bash.exe"), StringComparison.OrdinalIgnoreCase))
            {
                if (spec.Arguments.SequenceEqual(["--version"]))
                {
                    return Task.FromResult(Msys2Installed
                        ? Result(spec, 0, "GNU bash, version 5.2.37")
                        : Result(spec, 1, error: "bash missing"));
                }

                if (spec.Arguments.Contains("-lc"))
                {
                    Msys2Installed = true;
                    GccInstalled = true;
                    MakeInstalled = true;
                    return Task.FromResult(Result(spec, 0, "pacman ok"));
                }
            }

            if (Matches(spec.FileName, Path.Combine(ProductInfo.DefaultMsys2UcrtBin, "gcc.exe"), "gcc"))
            {
                return Task.FromResult(GccInstalled
                    ? Result(spec, 0, "gcc.exe (Rev3, Built by MSYS2 project) 13.2.0")
                    : Result(spec, 1, error: "gcc missing"));
            }

            if (Matches(spec.FileName, Path.Combine(ProductInfo.DefaultMsys2UcrtBin, "make.exe"), "make"))
            {
                return Task.FromResult(MakeInstalled
                    ? Result(spec, 0, "GNU Make 4.4.1")
                    : Result(spec, 1, error: "make missing"));
            }

            if (string.Equals(spec.FileName, codeCmd, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(spec.FileName, codeExe, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(VsCodeInstalled
                    ? Result(spec, 0, "1.100.0")
                    : Result(spec, 1, error: "code missing"));
            }

            return Task.FromResult(Result(spec, 0, "ok"));
        }

        private static bool Matches(string actual, string fullPath, string commandName)
        {
            return string.Equals(actual, fullPath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Path.GetFileName(actual), $"{commandName}.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, commandName, StringComparison.OrdinalIgnoreCase);
        }

        private static CommandResult Result(CommandSpec spec, int exitCode, string output = "", string error = "") => new()
        {
            Spec = spec,
            ExitCode = exitCode,
            StandardOutput = output,
            StandardError = error,
            Duration = TimeSpan.FromMilliseconds(1)
        };
    }
}
