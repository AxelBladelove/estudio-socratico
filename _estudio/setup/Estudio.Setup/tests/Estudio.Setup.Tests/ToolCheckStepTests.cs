using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class ToolCheckStepTests
{
    [Fact]
    public async Task DetectAsync_returns_ok_when_version_command_succeeds()
    {
        var runner = new FakeCommandRunner(CommandResult.Success("git version 2.50.0"));
        var step = new ToolCheckStep("git", "Git", "git", "--version", runner);

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.IsMissing);
        Assert.Contains("git version", result.Message);
        Assert.Equal(("git", "--version"), runner.Calls.Single());
    }

    [Fact]
    public async Task DetectAsync_uses_first_non_empty_output_line()
    {
        var output = $"{Environment.NewLine} Pacman v6.1.0{Environment.NewLine}extra";
        var runner = new FakeCommandRunner(CommandResult.Success(output));
        var step = new ToolCheckStep("msys2-pacman", "MSYS2 Pacman", "pacman", "--version", runner);

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Pacman v6.1.0", result.Message);
    }

    [Fact]
    public async Task DetectAsync_returns_missing_when_executable_cannot_be_started()
    {
        var runner = new FakeCommandRunner(CommandResult.NotFound("gh"));
        var step = new ToolCheckStep("gh", "GitHub CLI", "gh", "--version", runner);

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("gh", result.Message);
    }

    [Fact]
    public async Task DetectAsync_reports_absolute_missing_path_without_path_wording()
    {
        var fileName = @"C:\msys64\ucrt64\bin\gcc.exe";
        var runner = new FakeCommandRunner(CommandResult.NotFound(fileName));
        var step = new ToolCheckStep("gcc", "GCC UCRT64", fileName, "--version", runner);

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
        Assert.DoesNotContain("PATH", result.Message);
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        private readonly CommandResult _result;
        private readonly List<(string FileName, string Arguments)> _calls = new();

        public FakeCommandRunner(CommandResult result)
        {
            _result = result;
        }

        public IReadOnlyList<(string FileName, string Arguments)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(_result);
        }
    }
}
