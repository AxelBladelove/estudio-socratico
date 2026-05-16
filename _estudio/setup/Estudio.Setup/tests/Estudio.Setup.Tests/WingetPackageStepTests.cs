using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class WingetPackageStepTests
{
    [Fact]
    public async Task InstallAsync_runs_winget_install_for_package_id()
    {
        var runner = new FakeCommandRunner(CommandResult.Success("instalado"));
        var step = new WingetPackageStep(
            id: "git",
            name: "Git",
            packageId: "Git.Git",
            fileName: "git",
            versionArguments: "--version",
            runner);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        var call = runner.Calls.Single();
        Assert.Equal("winget", call.FileName);
        Assert.Contains("install", call.Arguments);
        Assert.Contains("Git.Git", call.Arguments);
        Assert.Contains("--accept-package-agreements", call.Arguments);
    }

    [Fact]
    public async Task UpdateAsync_runs_winget_upgrade_for_package_id()
    {
        var runner = new FakeCommandRunner(CommandResult.Success("actualizado"));
        var step = new WingetPackageStep(
            id: "github-cli",
            name: "GitHub CLI",
            packageId: "GitHub.cli",
            fileName: "gh",
            versionArguments: "--version",
            runner);

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        var call = runner.Calls.Single();
        Assert.Equal("winget", call.FileName);
        Assert.Contains("upgrade", call.Arguments);
        Assert.Contains("GitHub.cli", call.Arguments);
    }

    [Fact]
    public async Task InstallAsync_reports_failure_when_winget_fails()
    {
        var runner = new FakeCommandRunner(CommandResult.Failure(1, string.Empty, "package not found"));
        var step = new WingetPackageStep(
            id: "node",
            name: "Node.js",
            packageId: "OpenJS.NodeJS.LTS",
            fileName: "node",
            versionArguments: "--version",
            runner);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.IsMissing);
        Assert.Contains("package not found", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_returns_warning_when_winget_upgrade_fails_but_tool_still_verifies()
    {
        var runner = new FakeCommandRunner(
            CommandResult.Failure(-1978335212, string.Empty, "winget upgrade failed"),
            CommandResult.Success("git version 2.49.0.windows.1"));
        var step = new WingetPackageStep(
            id: "git",
            name: "Git",
            packageId: "Git.Git",
            fileName: "git",
            versionArguments: "--version",
            runner);

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.IsWarning);
        Assert.Contains("winget upgrade", result.Message);
        Assert.Contains("git version 2.49.0.windows.1", result.Message);
        Assert.Equal(
            new[]
            {
                ("winget", "upgrade --exact --id Git.Git --silent --accept-package-agreements --accept-source-agreements"),
                ("git", "--version"),
            },
            runner.Calls);
    }

    [Fact]
    public async Task RepairAsync_returns_warning_when_winget_install_fails_but_tool_still_verifies()
    {
        var runner = new FakeCommandRunner(
            CommandResult.Failure(-1978335189, string.Empty, string.Empty),
            CommandResult.Success("git version 2.54.0.windows.1"));
        var step = new WingetPackageStep(
            id: "git",
            name: "Git",
            packageId: "Git.Git",
            fileName: "git",
            versionArguments: "--version",
            runner);

        var result = await step.RepairAsync(new SetupContext(new SetupOptions(SetupMode.Reinstall)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.IsWarning);
        Assert.Contains("winget install", result.Message);
        Assert.Contains("git version 2.54.0.windows.1", result.Message);
        Assert.Equal(
            new[]
            {
                ("winget", "install --exact --id Git.Git --silent --accept-package-agreements --accept-source-agreements"),
                ("git", "--version"),
            },
            runner.Calls);
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        private readonly Queue<CommandResult> _results;
        private readonly List<(string FileName, string Arguments)> _calls = new();

        public FakeCommandRunner(params CommandResult[] results)
        {
            _results = new Queue<CommandResult>(results);
        }

        public IReadOnlyList<(string FileName, string Arguments)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(_results.Count == 0 ? CommandResult.Failure(99, string.Empty, "unexpected command") : _results.Dequeue());
        }
    }
}
