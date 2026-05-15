using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class DefaultSetupStepsTests
{
    [Fact]
    public void Create_returns_command_backed_prerequisite_steps_in_stable_order()
    {
        var steps = DefaultSetupSteps.Create(new NoopCommandRunner()).ToArray();

        Assert.Equal(
            new[]
            {
                "git",
                "git-safety-backup",
                "local-alias",
                "github-cli",
                "github-auth",
                "github-fork",
                "git-remotes",
                "git-project-update",
                "node",
                "vscode",
                "vscode-settings",
                "vscode-extension-package",
                "vscode-extension",
                "powershell7",
                "msys2-toolchain",
                "user-path",
                "gemini-runtime-config",
                "exercise-catalog",
            },
            steps.Select(step => step.Id));
    }

    [Fact]
    public async Task Msys2_toolchain_step_checks_standard_msys2_paths()
    {
        var runner = new RecordingCommandRunner();
        var pacman = DefaultSetupSteps.Create(runner).Single(step => step.Id == "msys2-toolchain");

        await pacman.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.Equal((@"C:\msys64\usr\bin\pacman.exe", "--version"), runner.Calls[0]);
        Assert.Equal((@"C:\msys64\ucrt64\bin\gcc.exe", "--version"), runner.Calls[1]);
        Assert.Equal((@"C:\msys64\ucrt64\bin\mingw32-make.exe", "--version"), runner.Calls[2]);
        Assert.Equal((@"C:\msys64\ucrt64\bin\gdb.exe", "--version"), runner.Calls[3]);
    }

    private sealed class NoopCommandRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandResult.Success(string.Empty));
        }
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        private readonly List<(string FileName, string Arguments)> _calls = new();

        public IReadOnlyList<(string FileName, string Arguments)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(CommandResult.Success(string.Empty));
        }
    }
}
