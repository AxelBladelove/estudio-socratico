using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class VsixExtensionStepTests
{
    [Fact]
    public async Task InstallAsync_runs_code_install_extension_with_force()
    {
        var vsixPath = CreateVsix();
        var runner = new QueueCommandRunner(CommandResult.Success("installed"));
        var step = new VsixExtensionStep(vsixPath, "estudio-socratico.estudio-exercism", runner);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        var call = runner.Calls.Single();
        Assert.Equal("code", call.FileName);
        Assert.Contains("--install-extension", call.Arguments);
        Assert.Contains(vsixPath, call.Arguments);
        Assert.Contains("--force", call.Arguments);
    }

    [Fact]
    public async Task DetectAsync_returns_missing_when_vsix_file_is_absent()
    {
        var step = new VsixExtensionStep(
            Path.Combine(MakeTempRoot(), "missing.vsix"),
            "estudio-socratico.estudio-exercism",
            new QueueCommandRunner());

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public async Task VerifyAsync_succeeds_when_extension_is_listed()
    {
        var runner = new QueueCommandRunner(CommandResult.Success("ms-vscode.cpptools\r\nestudio-socratico.estudio-exercism\r\n"));
        var step = new VsixExtensionStep(CreateVsix(), "estudio-socratico.estudio-exercism", runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("code", "--list-extensions"), runner.Calls.Single());
    }

    [Fact]
    public async Task VerifyAsync_returns_missing_when_extension_is_not_listed()
    {
        var runner = new QueueCommandRunner(CommandResult.Success("ms-vscode.cpptools\r\n"));
        var step = new VsixExtensionStep(CreateVsix(), "estudio-socratico.estudio-exercism", runner);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public async Task UninstallAsync_runs_code_uninstall_extension()
    {
        var runner = new QueueCommandRunner(CommandResult.Success("uninstalled"));
        var step = new VsixExtensionStep(CreateVsix(), "estudio-socratico.estudio-exercism", runner);

        var result = await step.UninstallAsync(new SetupContext(new SetupOptions(SetupMode.Uninstall)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("code", "--uninstall-extension estudio-socratico.estudio-exercism"), runner.Calls.Single());
    }

    private static string CreateVsix()
    {
        var path = Path.Combine(MakeTempRoot(), "estudio-exercism.vsix");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fake vsix");
        return path;
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
