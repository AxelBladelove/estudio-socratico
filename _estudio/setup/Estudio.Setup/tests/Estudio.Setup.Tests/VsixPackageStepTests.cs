using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class VsixPackageStepTests
{
    [Fact]
    public async Task DetectAsync_succeeds_when_runtime_vsix_exists()
    {
        var root = MakeWorkspaceWithExtensionProject();
        var runtimeVsix = VsixExtensionPaths.ResolveVsixPath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeVsix)!);
        await File.WriteAllTextAsync(runtimeVsix, "runtime vsix");
        var step = new VsixPackageStep(root, new QueueCommandRunner());

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task InstallAsync_packages_extension_and_copies_vsix_to_runtime_path()
    {
        var root = MakeWorkspaceWithExtensionProject();
        var packagedVsix = VsixExtensionPaths.ResolvePackagedVsixPath(root);
        await File.WriteAllTextAsync(packagedVsix, "packaged vsix");
        var runner = new QueueCommandRunner(CommandResult.Success("ci"), CommandResult.Success("package"));
        var step = new VsixPackageStep(root, runner);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(VsixExtensionPaths.ResolveVsixPath(root)));
        Assert.Equal("packaged vsix", await File.ReadAllTextAsync(VsixExtensionPaths.ResolveVsixPath(root)));
        Assert.Equal(
            new[]
            {
                ("npm", $"ci --prefix {Quote(VsixExtensionPaths.ResolveExtensionSourceDirectory(root))}"),
                ("npm", $"run package --prefix {Quote(VsixExtensionPaths.ResolveExtensionSourceDirectory(root))}"),
            },
            runner.Calls);
    }

    [Fact]
    public async Task InstallAsync_returns_missing_when_extension_source_is_absent()
    {
        var root = MakeTempRoot();
        var step = new VsixPackageStep(root, new QueueCommandRunner());

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
    }

    private static string MakeWorkspaceWithExtensionProject()
    {
        var root = MakeTempRoot();
        var source = VsixExtensionPaths.ResolveExtensionSourceDirectory(root);
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "package.json"), "{}");

        return root;
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }

    private static string Quote(string value) => $"\"{value}\"";

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
