using Estudio.Setup.Release;

namespace Estudio.Setup.Tests;

public sealed class ReleasePackagerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-release-{Guid.NewGuid():N}");

    [Fact]
    public async Task CreateAsync_publishes_executable_copies_wrapper_writes_manifest_and_zip()
    {
        var projectPath = Path.Combine(_tempRoot, "src", "Estudio.Setup.csproj");
        var wrapperPath = Path.Combine(_tempRoot, "Estudio.Setup.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        await File.WriteAllTextAsync(projectPath, "<Project />");
        await File.WriteAllTextAsync(wrapperPath, "@echo off");
        ReleasePackageContext? publishContext = null;
        ReleasePackageContext? textualContext = null;
        var packager = new ReleasePackager(
            async (context, _) =>
            {
                publishContext = context;
                await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.exe"), "fake exe");
                await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.pdb"), "debug symbols");
            },
            async (context, _) =>
            {
                textualContext = context;
                await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.Textual.exe"), "fake textual exe");
            });

        var result = await packager.CreateAsync(
            new ReleasePackageRequest(
                ProjectPath: projectPath,
                WrapperPath: wrapperPath,
                OutputRoot: Path.Combine(_tempRoot, "release"),
                Version: "2.0.0",
                RuntimeIdentifier: "win-x64"),
            CancellationToken.None);

        Assert.NotNull(publishContext);
        Assert.NotNull(textualContext);
        Assert.Equal("Release", publishContext.Configuration);
        Assert.Equal("win-x64", publishContext.RuntimeIdentifier);
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.exe")));
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.Textual.exe")));
        Assert.False(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.pdb")));
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.cmd")));
        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(result.ZipPath));
        Assert.Contains("Estudio.Setup.exe", File.ReadAllText(result.ManifestPath));
        Assert.Contains("Estudio.Setup.Textual.exe", File.ReadAllText(result.ManifestPath));
        Assert.Contains("Estudio.Setup.cmd", result.Files);
        var readme = await File.ReadAllTextAsync(Path.Combine(result.PackageDirectory, "README.txt"));
        Assert.Contains("install --tui", readme);
        Assert.Contains("reinstall --tui", readme);
        Assert.Contains("uninstall", readme);
        Assert.Contains("verify", readme);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
