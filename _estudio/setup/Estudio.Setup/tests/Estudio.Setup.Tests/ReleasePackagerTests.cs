using System.IO.Compression;
using System.Text.Json;
using Estudio.Setup.Services;
using Estudio.Setup.Release;
using Estudio.Setup.State;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public sealed class ReleasePackagerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-release-{Guid.NewGuid():N}");

    [Fact]
    public async Task CreateAsync_builds_clean_release_layout_with_single_visible_installer()
    {
        var projectPath = Path.Combine(_tempRoot, "src", "Estudio.Setup.Windows.csproj");
        var wrapperPath = Path.Combine(_tempRoot, "Estudio.Setup.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        await File.WriteAllTextAsync(projectPath, "<Project />");
        await File.WriteAllTextAsync(wrapperPath, "@echo off");
        ReleasePackageContext? publishContext = null;
        var packager = new ReleasePackager(async (context, _) =>
        {
            publishContext = context;
            await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.Windows.exe"), "fake exe");
            await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.Windows.pdb"), "debug symbols");
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
        Assert.Equal("Release", publishContext.Configuration);
        Assert.Equal("win-x64", publishContext.RuntimeIdentifier);
        Assert.True(File.Exists(Path.Combine(result.PackageDirectory, SetupPackageLayout.InstallerExecutableFileName)));
        Assert.False(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.Windows.pdb")));
        Assert.False(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.Windows.exe")));
        Assert.False(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.runtimeconfig.json")));
        Assert.False(File.Exists(Path.Combine(result.PackageDirectory, "Estudio.Setup.cmd")));
        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(result.ZipPath));
        Assert.Equal(Path.Combine(result.PackageDirectory, SetupPackageLayout.PayloadDirectoryName, SetupPackageLayout.ManifestFileName), result.ManifestPath);
        Assert.Contains(SetupPackageLayout.InstallerExecutableFileName, result.Files);
        Assert.Contains($"{SetupPackageLayout.PayloadDirectoryName}/{SetupPackageLayout.ManifestFileName}", result.Files);
        Assert.Contains($"{SetupPackageLayout.PayloadDirectoryName}/{SetupPackageLayout.ChecksumsFileName}", result.Files);
        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        var manifestPaths = manifest.RootElement
            .GetProperty("Files")
            .EnumerateArray()
            .Select(element => element.GetProperty("Path").GetString())
            .OfType<string>()
            .ToArray();
        Assert.Contains(SetupPackageLayout.InstallerExecutableFileName, manifestPaths);
        var readme = await File.ReadAllTextAsync(Path.Combine(result.PackageDirectory, SetupPackageLayout.ReadmeFileName));
        Assert.Contains(SetupPackageLayout.InstallerExecutableFileName, readme);
        Assert.Contains(SetupPackageLayout.PayloadDirectoryName, readme);
        Assert.EndsWith("EstudioSocratico-2.0.0-win-x64.zip", result.ZipPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_packages_framework_inside_payload_without_leaking_repo_root()
    {
        var workspaceRoot = Path.Combine(_tempRoot, "workspace");
        var projectPath = Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "src", "Estudio.Setup.Windows", "Estudio.Setup.Windows.csproj");
        var wrapperPath = Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(wrapperPath)!);
        await File.WriteAllTextAsync(projectPath, "<Project />");
        await File.WriteAllTextAsync(wrapperPath, "@echo off");
        await WriteWorkspaceFile(workspaceRoot, "README.md", "# Estudio");
        await WriteWorkspaceFile(workspaceRoot, "AGENTS.md", "# Agentes");
        await WriteWorkspaceFile(workspaceRoot, ".vscode/tasks.json", "{}");
        await WriteWorkspaceFile(workspaceRoot, "Ejercicios/README.md", "# Ejercicios");
        await WriteWorkspaceFile(workspaceRoot, "Instalar Estudio Socratico.cmd", "@echo off");
        await WriteWorkspaceFile(workspaceRoot, "Actualizar Estudio Socratico.cmd", "@echo off");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/soporte/vscode/estudio-exercism/package.json", "{}");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/soporte/exercism/catalogs/alejandro.json", "[]");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/soporte/runtime/secret.txt", "runtime");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/soporte/runtime/vscode/estudio-exercism.vsix", "vsix");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/setup/runtime-config.private.json", "{\"apiKey\":\"test-runtime-key\"}");
        await WriteWorkspaceFile(workspaceRoot, "_estudio/setup/runtime-config.bootstrap.json", "{\"runtimeConfigUrl\":\"https://example.com/runtime.json\"}");
        await WriteWorkspaceFile(workspaceRoot, ".estudio-drive/token.json", "{}");
        await WriteWorkspaceFile(workspaceRoot, "02-gemini-api-key-runtime-config-2.md", "draft");
        await WriteWorkspaceFile(workspaceRoot, "soporte/runtime/old.txt", "legacy");
        var packager = new ReleasePackager(async (context, _) =>
        {
            await File.WriteAllTextAsync(Path.Combine(context.PackageDirectory, "Estudio.Setup.Windows.exe"), "fake exe");
        });

        var result = await packager.CreateAsync(
            new ReleasePackageRequest(
                ProjectPath: projectPath,
                WrapperPath: wrapperPath,
                OutputRoot: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "publish", "release"),
                Version: "2.0.0",
                RuntimeIdentifier: "win-x64"),
            CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(result.PackageDirectory, "_estudio")));
        Assert.False(Directory.Exists(Path.Combine(result.PackageDirectory, ".vscode")));
        Assert.False(Directory.Exists(Path.Combine(result.PackageDirectory, "Ejercicios")));
        Assert.False(File.Exists(Path.Combine(result.PackageDirectory, "package.json")));

        Assert.Contains($"{SetupPackageLayout.PayloadDirectoryName}/{SetupPackageLayout.FrameworkArchiveFileName}", result.Files);
        Assert.Contains($"{SetupPackageLayout.PayloadDirectoryName}/{VsixExtensionPaths.ReleasePackageFileName}", result.Files);
        Assert.Contains($"{SetupPackageLayout.PayloadDirectoryName}/runtime-config.bootstrap.json", result.Files);
        Assert.DoesNotContain($"{SetupPackageLayout.PayloadDirectoryName}/runtime-config.private.json", result.Files);
        Assert.DoesNotContain("Instalar Estudio Socratico.cmd", result.Files);
        Assert.DoesNotContain("Actualizar Estudio Socratico.cmd", result.Files);
        Assert.DoesNotContain("_estudio/soporte/runtime/secret.txt", result.Files);
        Assert.DoesNotContain(".estudio-drive/token.json", result.Files);
        Assert.DoesNotContain("02-gemini-api-key-runtime-config-2.md", result.Files);
        Assert.DoesNotContain("soporte/runtime/old.txt", result.Files);

        using var framework = ZipFile.OpenRead(Path.Combine(result.PackageDirectory, SetupPackageLayout.PayloadDirectoryName, SetupPackageLayout.FrameworkArchiveFileName));
        var entries = framework.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
        Assert.Contains("README.md", entries);
        Assert.Contains(".vscode/tasks.json", entries);
        Assert.Contains("Ejercicios/README.md", entries);
        Assert.Contains("_estudio/soporte/vscode/estudio-exercism/package.json", entries);
        Assert.Contains("_estudio/soporte/exercism/catalogs/alejandro.json", entries);
        Assert.DoesNotContain("_estudio/soporte/runtime/secret.txt", entries);
        Assert.DoesNotContain(".estudio-drive/token.json", entries);
        Assert.DoesNotContain("02-gemini-api-key-runtime-config-2.md", entries);
        Assert.DoesNotContain("soporte/runtime/old.txt", entries);
    }

    [Fact]
    public void ForWorkspace_targets_windows_ui_project()
    {
        var request = ReleasePackager.ForWorkspace("C:\\repo");

        Assert.EndsWith("src\\Estudio.Setup.Windows\\Estudio.Setup.Windows.csproj", request.ProjectPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FileSetupStateStore.CurrentSetupVersion, request.Version);
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
