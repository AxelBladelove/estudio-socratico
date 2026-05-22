using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class GitHubLoginTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppPaths _paths;
    private readonly string _ghExe;
    private readonly string _wingetExe;
    private readonly string _gitExe;

    public GitHubLoginTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"estudio-ghlogin-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _paths = new AppPaths(repoRoot: _tempDir, localAppDataRoot: Path.Combine(_tempDir, "LocalAppData"));

        _ghExe = Path.Combine(_tempDir, "gh.exe");
        _wingetExe = Path.Combine(_tempDir, "winget.exe");
        _gitExe = Path.Combine(_tempDir, "git.exe");

        File.WriteAllText(_ghExe, "");
        File.WriteAllText(_wingetExe, "");
        File.WriteAllText(_gitExe, "");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private sealed class TestCommandRunner : ICommandRunner
    {
        private readonly string _ghExe;
        private readonly string _wingetExe;
        private readonly string _gitExe;

        public bool GhInstalled { get; set; }
        public bool AuthLoginCalled { get; private set; }
        public bool InstallGhCalled { get; private set; }
        public bool IsLoggedIn { get; set; }

        public TestCommandRunner(string ghExe, string wingetExe, string gitExe, bool initialGhInstalled)
        {
            _ghExe = ghExe;
            _wingetExe = wingetExe;
            _gitExe = gitExe;
            GhInstalled = initialGhInstalled;
            IsLoggedIn = initialGhInstalled;
        }

        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(spec.FileName).ToLowerInvariant();

            // Intercept where.exe
            if (fileName == "where.exe")
            {
                var query = spec.Arguments.FirstOrDefault();
                if (query == "gh")
                {
                    if (GhInstalled)
                    {
                        return Task.FromResult(new CommandResult
                        {
                            Spec = spec,
                            ExitCode = 0,
                            StandardOutput = _ghExe + Environment.NewLine
                        });
                    }
                    else
                    {
                        return Task.FromResult(new CommandResult
                        {
                            Spec = spec,
                            ExitCode = 1,
                            StandardError = "INFO: Could not find files for the given pattern(s)."
                        });
                    }
                }

                if (query == "winget")
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = 0,
                        StandardOutput = _wingetExe + Environment.NewLine
                    });
                }

                if (query == "git")
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = 0,
                        StandardOutput = _gitExe + Environment.NewLine
                    });
                }
            }

            // Intercept winget.exe
            if (fileName == "winget" || fileName == "winget.exe")
            {
                if (spec.Arguments.Contains("--info"))
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = 0,
                        StandardOutput = "Windows Package Manager v1.8"
                    });
                }

                if (spec.Arguments.Contains("source") && spec.Arguments.Contains("list"))
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = 0,
                        StandardOutput = "winget          https://cdn.winget.microsoft.com/cache"
                    });
                }

                if (spec.Arguments.Contains("install") && spec.Arguments.Contains("GitHub.cli"))
                {
                    InstallGhCalled = true;
                    GhInstalled = true;
                    return Task.FromResult(new CommandResult { Spec = spec, ExitCode = 0 });
                }
            }

            // Intercept gh commands
            if (fileName == "gh" || fileName == "gh.exe" || spec.FileName == _ghExe)
            {
                if (spec.Arguments.Contains("--version"))
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = 0,
                        StandardOutput = "gh version 2.45.0"
                    });
                }

                if (spec.Arguments.SequenceEqual(new[] { "auth", "status", "--hostname", "github.com" }))
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = IsLoggedIn ? 0 : 1,
                        StandardOutput = IsLoggedIn ? "Logged in to github.com" : "Not logged in"
                    });
                }

                if (spec.Arguments.Contains("login"))
                {
                    AuthLoginCalled = true;
                    IsLoggedIn = true;
                    return Task.FromResult(new CommandResult { Spec = spec, ExitCode = 0 });
                }

                if (spec.Arguments.Contains("user"))
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = 0,
                        StandardOutput = "test-user\n"
                    });
                }

                return Task.FromResult(new CommandResult { Spec = spec, ExitCode = 0 });
            }

            // Intercept git commands
            if (fileName == "git" || fileName == "git.exe" || spec.FileName == _gitExe)
            {
                if (spec.Arguments.Contains("--version"))
                {
                    return Task.FromResult(new CommandResult
                    {
                        Spec = spec,
                        ExitCode = 0,
                        StandardOutput = "git version 2.45.0"
                    });
                }
                return Task.FromResult(new CommandResult { Spec = spec, ExitCode = 0 });
            }

            return Task.FromResult(new CommandResult { Spec = spec, ExitCode = 0 });
        }
    }

    private ConfiguratorEngine CreateEngine(TestCommandRunner runner)
    {
        return new ConfiguratorEngine(_paths, runner, path =>
        {
            if (path.Contains("GitHub CLI", StringComparison.OrdinalIgnoreCase))
            {
                return runner.GhInstalled;
            }
            if (path.Contains("Git", StringComparison.OrdinalIgnoreCase) || 
                path.Contains("nodejs", StringComparison.OrdinalIgnoreCase) || 
                path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (path.StartsWith(_tempDir, StringComparison.OrdinalIgnoreCase))
            {
                if (path == _ghExe) return runner.GhInstalled;
                return File.Exists(path);
            }
            return File.Exists(path);
        });
    }

    [Fact]
    public async Task FreshSetup_GhMissing_InstallsGhBeforeLogin()
    {
        var runner = new TestCommandRunner(_ghExe, _wingetExe, _gitExe, initialGhInstalled: false);
        var engine = CreateEngine(runner);

        var result = await engine.ConfigureGitHubAsync(switchAccount: false, workspacePath: _tempDir, installGh: true);

        Assert.True(runner.InstallGhCalled);
        Assert.True(runner.GhInstalled);
        Assert.Equal("test-user", result.UserName);
    }

    [Fact]
    public async Task FreshSetup_DoesNotAllowGithubLoginBeforeGhReady()
    {
        var runner = new TestCommandRunner(_ghExe, _wingetExe, _gitExe, initialGhInstalled: false);
        var engine = CreateEngine(runner);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ConfigureGitHubAsync(switchAccount: false, workspacePath: _tempDir, installGh: false));

        Assert.Contains("Primero necesitamos instalar GitHub CLI", ex.Message);
        Assert.False(runner.InstallGhCalled);
        Assert.False(runner.AuthLoginCalled);
    }

    [Fact]
    public async Task GithubLogin_AfterGhInstall_CallsAuthLogin()
    {
        var runner = new TestCommandRunner(_ghExe, _wingetExe, _gitExe, initialGhInstalled: false);
        var engine = CreateEngine(runner);

        var result = await engine.ConfigureGitHubAsync(switchAccount: false, workspacePath: _tempDir, installGh: true);

        Assert.True(runner.InstallGhCalled);
        Assert.True(runner.AuthLoginCalled);
        Assert.Equal("test-user", result.UserName);
    }

    [Fact]
    public async Task FreshInstall_GhMissing_CanContinueAfterInstall()
    {
        var runner = new TestCommandRunner(_ghExe, _wingetExe, _gitExe, initialGhInstalled: true);
        var engine = CreateEngine(runner);

        var result = await engine.ConfigureGitHubAsync(switchAccount: false, workspacePath: _tempDir, installGh: false);

        Assert.False(runner.InstallGhCalled);
        Assert.True(runner.GhInstalled);
        Assert.Equal("test-user", result.UserName);
    }
}
