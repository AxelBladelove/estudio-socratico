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

    [Fact]
    public async Task CreateAsync_copies_clean_workspace_payload_for_standalone_installer()
    {
        var workspaceRoot = Path.Combine(_tempRoot, "workspace");
        var projectPath = Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "src", "Estudio.Setup", "Estudio.Setup.csproj");
        var wrapperPath = Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(wrapperPath)!);
        await File.WriteAllTextAsync(projectPath, "<Project />");
        await File.WriteAllTextAsync(wrapperPath, "@echo off");
        await WriteWorkspaceFile(workspaceRoot, "README.md", "# Estudio");
        await WriteWorkspaceFile(workspaceRoot, "AGENTS.md", "# Agentes");
        await WriteWorkspaceFile(workspaceRoot, ".vscode/tasks.json", "{}");
        await WriteWorkspaceFile(workspaceRoot, "Ejercicios/README.md", "# Ejercicios");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/soporte/vscode/estudio-exercism/package.json", "{}");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/soporte/exercism/catalogs/alejandro.json", "[]");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/soporte/runtime/secret.txt", "runtime");
        await WriteWorkspaceFile(workspaceRoot, ".estudio-drive/token.json", "{}");
        await WriteWorkspaceFile(workspaceRoot, "02-gemini-api-key-runtime-config-2.md", "draft");
        await WriteWorkspaceFile(workspaceRoot, "soporte/runtime/old.txt", "legacy");
        var packager = new ReleasePackager(
            async (context, _) =>
            {
                await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.exe"), "fake exe");
            },
            async (context, _) =>
            {
                await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.Textual.exe"), "fake textual exe");
            });

        var result = await packager.CreateAsync(
            new ReleasePackageRequest(
                ProjectPath: projectPath,
                WrapperPath: wrapperPath,
                OutputRoot: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "publish", "release"),
                Version: "2.0.0",
                RuntimeIdentifier: "win-x64"),
            CancellationToken.None);

        Assert.Contains("_estudio/soporte/vscode/estudio-exercism/package.json", result.Files);
        Assert.Contains("_estudio/soporte/exercism/catalogs/alejandro.json", result.Files);
        Assert.Contains(".vscode/tasks.json", result.Files);
        Assert.Contains("Ejercicios/README.md", result.Files);
        Assert.Contains("README.md", result.Files);
        Assert.DoesNotContain("_estudio/soporte/runtime/secret.txt", result.Files);
        Assert.DoesNotContain(".estudio-drive/token.json", result.Files);
        Assert.DoesNotContain("02-gemini-api-key-runtime-config-2.md", result.Files);
        Assert.DoesNotContain("soporte/runtime/old.txt", result.Files);
    }

    private static async Task WriteWorkspaceFile(string workspaceRoot, string relativePath, string contents)
    {
        var path = Path.Combine(
            new[] { workspaceRoot }
                .Concat(relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
