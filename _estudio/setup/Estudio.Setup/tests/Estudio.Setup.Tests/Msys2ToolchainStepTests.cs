using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class Msys2ToolchainStepTests
{
    [Fact]
    public async Task InstallAsync_installs_msys2_when_pacman_is_missing_then_installs_toolchain()
    {
        var runner = new QueueCommandRunner(
            CommandResult.NotFound(Msys2ToolchainStep.PacmanPath),
            CommandResult.Success("msys2 installed"),
            CommandResult.Success("pacman"),
            CommandResult.NotFound(Msys2ToolchainStep.GccPath),
            CommandResult.Success("system updated"),
            CommandResult.Success("toolchain installed"));
        var step = new Msys2ToolchainStep(runner, MakeTempRoot());

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("winget", runner.Calls[1].FileName);
        Assert.Contains("MSYS2.MSYS2", runner.Calls[1].Arguments);
        Assert.Equal(Msys2ToolchainStep.PacmanPath, runner.Calls[2].FileName);
        Assert.Equal(Msys2ToolchainStep.GccPath, runner.Calls[3].FileName);
        Assert.Equal(Msys2ToolchainStep.PacmanPath, runner.Calls[4].FileName);
        Assert.Contains("-Syu --noconfirm", runner.Calls[4].Arguments);
        Assert.Equal(Msys2ToolchainStep.PacmanPath, runner.Calls[5].FileName);
        Assert.Contains("mingw-w64-ucrt-x86_64-toolchain", runner.Calls[5].Arguments);
    }

    [Fact]
    public async Task UpdateAsync_uses_pacman_without_reinstalling_msys2_when_pacman_exists()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("pacman"),
            CommandResult.Success("pacman"),
            CommandResult.Success("gcc"),
            CommandResult.Success("make"),
            CommandResult.Success("gdb"),
            CommandResult.Success("system updated"),
            CommandResult.Success("toolchain installed"));
        var step = new Msys2ToolchainStep(runner, MakeTempRoot());

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(runner.Calls, call => call.FileName == "winget");
        Assert.Equal(5, runner.Calls.Count);
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.Contains("-Syu --noconfirm", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepairAsync_skips_pacman_sync_when_toolchain_is_already_healthy()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("pacman"),
            CommandResult.Success("pacman"),
            CommandResult.Success("gcc"),
            CommandResult.Success("make"),
            CommandResult.Success("gdb"));
        var step = new Msys2ToolchainStep(runner, MakeTempRoot());

        var result = await step.RepairAsync(new SetupContext(new SetupOptions(SetupMode.Reinstall)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("no requirio reinstalar", result.Message);
        Assert.Equal(5, runner.Calls.Count);
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.Contains("-Syu --noconfirm", StringComparison.Ordinal));
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.Contains("mingw-w64-ucrt-x86_64-toolchain", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallAsync_returns_warning_when_pacman_sync_fails_but_toolchain_still_verifies()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("pacman"),
            CommandResult.Success("pacman"),
            CommandResult.Success("gcc"),
            CommandResult.Success("make"),
            CommandResult.Success("gdb"),
            CommandResult.Failure(1, string.Empty, "ssl failed"),
            CommandResult.Success("pacman"),
            CommandResult.Success("gcc"),
            CommandResult.Success("make"),
            CommandResult.Success("gdb"));
        var step = new Msys2ToolchainStep(runner, MakeTempRoot());

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.IsWarning);
        Assert.Contains("pacman -Syu fallo", result.Message);
        Assert.Contains("toolchain UCRT64 detectado", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_checks_pacman_tools_and_compiles_real_program()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("pacman"),
            CommandResult.Success("gcc"),
            CommandResult.Success("make"),
            CommandResult.Success("gdb"),
            CommandResult.Success(string.Empty),
            CommandResult.Success("Estudio Socratico GCC OK"));
        var step = new Msys2ToolchainStep(runner, MakeTempRoot());

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(Msys2ToolchainStep.PacmanPath, runner.Calls[0].FileName);
        Assert.Equal(Msys2ToolchainStep.GccPath, runner.Calls[1].FileName);
        Assert.Equal(Msys2ToolchainStep.MakePath, runner.Calls[2].FileName);
        Assert.Equal(Msys2ToolchainStep.GdbPath, runner.Calls[3].FileName);
        Assert.Equal(Msys2ToolchainStep.GccPath, runner.Calls[4].FileName);
        Assert.EndsWith("hello_world.exe", runner.Calls[5].FileName);
    }

    [Fact]
    public async Task DetectAsync_reports_missing_when_ucrt64_gcc_is_missing()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("pacman"),
            CommandResult.NotFound(Msys2ToolchainStep.GccPath));
        var step = new Msys2ToolchainStep(runner, MakeTempRoot());

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("GCC", result.Message);
        Assert.Equal(Msys2ToolchainStep.PacmanPath, runner.Calls[0].FileName);
        Assert.Equal(Msys2ToolchainStep.GccPath, runner.Calls[1].FileName);
    }

    [Fact]
    public async Task VerifyAsync_reports_missing_when_gdb_is_missing()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("pacman"),
            CommandResult.Success("gcc"),
            CommandResult.Success("make"),
            CommandResult.NotFound(Msys2ToolchainStep.GdbPath));
        var step = new Msys2ToolchainStep(runner, MakeTempRoot());

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("GDB", result.Message);
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }

    private sealed class QueueCommandRunner : ICommandRunner
    {
        private readonly Queue<CommandResult> _results;
        private readonly List<(string FileName, string Arguments)> _calls = new();

        public QueueCommandRunner(params CommandResult[] results)
        {
            _results = new Queue<CommandResult>(results);
        }

        public IReadOnlyList<(string FileName, string Arguments)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(_results.Dequeue());
        }
    }
}
