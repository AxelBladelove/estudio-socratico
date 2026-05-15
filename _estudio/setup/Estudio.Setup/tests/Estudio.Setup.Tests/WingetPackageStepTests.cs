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
