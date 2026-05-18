using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Estudio.Setup.Runtime;
using Estudio.Setup.Services;
using Estudio.Setup.State;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Release;

public sealed class ReleasePackager
{
    private const string DefaultPublishedExecutableFileName = "Estudio.Setup.Windows.exe";

    private readonly Func<ReleasePackageContext, CancellationToken, Task> _publishAsync;
    private readonly Func<ReleasePackageContext, CancellationToken, Task> _buildLauncherAsync;

    public ReleasePackager(ICommandRunner commandRunner)
        : this(async (context, cancellationToken) =>
        {
            var arguments = string.Join(
                " ",
                "publish",
                Quote(context.ProjectPath),
                "-c",
                context.Configuration,
                "-r",
                context.RuntimeIdentifier,
                "--self-contained",
                "true",
                "-p:PublishSingleFile=true",
                "-p:IncludeNativeLibrariesForSelfExtract=true",
                "-p:EnableCompressionInSingleFile=true",
                "-p:DebugType=None",
                "-p:DebugSymbols=false",
                "-o",
                Quote(context.PackageDirectory));
            var result = await commandRunner.RunAsync("dotnet", arguments, cancellationToken);
            if (!result.WasStarted)
            {
                throw new InvalidOperationException("No se pudo ejecutar dotnet para empaquetar Estudio.Setup.");
            }

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"dotnet publish fallo. {FirstNonEmpty(result.StandardError, result.StandardOutput)}");
            }
        },
        (_, _) => Task.CompletedTask)
    {
    }

    public ReleasePackager(Func<ReleasePackageContext, CancellationToken, Task> publishAsync)
        : this(publishAsync, (_, _) => Task.CompletedTask)
    {
    }

    public ReleasePackager(
        Func<ReleasePackageContext, CancellationToken, Task> publishAsync,
        Func<ReleasePackageContext, CancellationToken, Task> buildLauncherAsync)
    {
        _publishAsync = publishAsync;
        _buildLauncherAsync = buildLauncherAsync;
    }

    public async Task<ReleasePackageResult> CreateAsync(
        ReleasePackageRequest request,
        CancellationToken cancellationToken)
    {
        var outputRoot = Path.GetFullPath(request.OutputRoot);
        Directory.CreateDirectory(outputRoot);
        var packageName = $"EstudioSocratico-{Sanitize(request.Version)}-{request.RuntimeIdentifier}";
        var packageDirectory = Path.Combine(outputRoot, packageName);
        var zipPath = Path.Combine(outputRoot, $"{packageName}.zip");
        DeleteGeneratedDirectory(packageDirectory, outputRoot);
        DeleteGeneratedFile(zipPath, outputRoot);
        Directory.CreateDirectory(packageDirectory);

        var context = new ReleasePackageContext(
            Path.GetFullPath(request.ProjectPath),
            Path.GetFullPath(request.WrapperPath),
            packageDirectory,
            request.Version,
            request.RuntimeIdentifier,
            "Release");
        await _publishAsync(context, cancellationToken);
        await _buildLauncherAsync(context, cancellationToken);

        var setupExe = Path.Combine(packageDirectory, ResolvePublishedExecutableFileName(context.ProjectPath));
        if (!File.Exists(setupExe))
        {
            throw new InvalidOperationException($"dotnet publish no genero {setupExe}.");
        }

        RemoveDebugSymbols(packageDirectory);
        PromoteInstallerExecutable(packageDirectory, setupExe);

        var workspaceRoot = ResolveWorkspaceRoot(request);
        var payloadRoot = SetupPackageLayout.ResolvePayloadRoot(packageDirectory);
        Directory.CreateDirectory(payloadRoot);
        CreateFrameworkArchive(workspaceRoot, payloadRoot);
        CopyBundledVsix(workspaceRoot, payloadRoot);
        CopyBundledRuntimeConfig(workspaceRoot, payloadRoot);
        RemoveDebugSymbols(packageDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(packageDirectory, SetupPackageLayout.ReadmeFileName),
            BuildReadme(request.Version),
            cancellationToken);
        PruneVisibleRoot(packageDirectory);

        var manifestPath = Path.Combine(payloadRoot, SetupPackageLayout.ManifestFileName);
        var checksumsPath = Path.Combine(payloadRoot, SetupPackageLayout.ChecksumsFileName);
        var manifestFiles = BuildFileEntries(packageDirectory, manifestPath, checksumsPath);
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new ReleaseManifest(
                    request.Version,
                    request.RuntimeIdentifier,
                    DateTimeOffset.UtcNow,
                    manifestFiles),
                new JsonSerializerOptions { WriteIndented = true },
                cancellationToken);
        }

        await File.WriteAllTextAsync(
            checksumsPath,
            BuildChecksums(manifestFiles),
            cancellationToken);

        var files = BuildFileEntries(packageDirectory);

        ZipFile.CreateFromDirectory(packageDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

        return new ReleasePackageResult(
            packageDirectory,
            zipPath,
            manifestPath,
            files.Select(file => file.Path).ToArray());
    }

    public static ReleasePackageRequest ForWorkspace(
        string workspaceRoot,
        string version = FileSetupStateStore.CurrentSetupVersion,
        string runtimeIdentifier = "win-x64")
    {
        return new ReleasePackageRequest(
            ProjectPath: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "src", "Estudio.Setup.Windows", "Estudio.Setup.Windows.csproj"),
            WrapperPath: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup.cmd"),
            OutputRoot: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "publish", "release"),
            Version: version,
            RuntimeIdentifier: runtimeIdentifier);
    }

    private static IReadOnlyList<ReleaseFileEntry> BuildFileEntries(string packageDirectory, params string[] excludedPaths)
    {
        var excluded = excludedPaths
            .Select(path => Path.GetRelativePath(packageDirectory, Path.GetFullPath(path)).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Directory
            .EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new
            {
                FullPath = path,
                RelativePath = Path.GetRelativePath(packageDirectory, path).Replace('\\', '/'),
            })
            .Where(file => !excluded.Contains(file.RelativePath))
            .Select(file => new ReleaseFileEntry(
                file.RelativePath,
                new FileInfo(file.FullPath).Length,
                Sha256(file.FullPath)))
            .ToArray();
    }

    private static void RemoveDebugSymbols(string packageDirectory)
    {
        foreach (var pdb in Directory.EnumerateFiles(packageDirectory, "*.pdb", SearchOption.AllDirectories))
        {
            File.Delete(pdb);
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string BuildReadme(string version)
    {
        return $"""
            Estudio Socratico Setup {version}

            1. Doble clic en {SetupPackageLayout.InstallerExecutableFileName}.
            2. El instalador creara tu workspace real en una carpeta limpia fuera de este ZIP.
            3. La carpeta payload contiene el framework empaquetado, la extension VS Code y hashes de verificacion.

            Si abres el instalador desde terminal, sin argumentos hace verify automaticamente.
            """;
    }

    private static string ResolveWorkspaceRoot(ReleasePackageRequest request)
    {
        var wrapperDirectory = Path.GetDirectoryName(Path.GetFullPath(request.WrapperPath));
        if (wrapperDirectory is not null
            && string.Equals(Path.GetFileName(wrapperDirectory), "setup", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFileName(Directory.GetParent(wrapperDirectory)?.FullName), "_estudio", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(wrapperDirectory)!.Parent!.FullName;
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(request.ProjectPath))
            ?? throw new InvalidOperationException("No se pudo resolver el proyecto Estudio.Setup.");
        return Path.GetFullPath(Path.Combine(projectDirectory, "..", "..", "..", "..", ".."));
    }

    private static void CreateFrameworkArchive(string workspaceRoot, string payloadRoot)
    {
        var stagingRoot = Path.Combine(payloadRoot, ".framework-staging");
        DeleteIfExists(stagingRoot);
        Directory.CreateDirectory(stagingRoot);
        try
        {
            CopyFrameworkPayload(workspaceRoot, stagingRoot);
            var frameworkPath = Path.Combine(payloadRoot, SetupPackageLayout.FrameworkArchiveFileName);
            DeleteIfExists(frameworkPath);
            ZipFile.CreateFromDirectory(stagingRoot, frameworkPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            DeleteIfExists(stagingRoot);
        }
    }

    private static void CopyFrameworkPayload(string workspaceRoot, string destinationRoot)
    {
        CopyRootFile(workspaceRoot, destinationRoot, "README.md");
        CopyRootFile(workspaceRoot, destinationRoot, "AGENTS.md");
        CopyRootFile(workspaceRoot, destinationRoot, "package.json");
        CopyRootFile(workspaceRoot, destinationRoot, ".gitignore");

        CopyDirectoryIfExists(Path.Combine(workspaceRoot, ".vscode"), Path.Combine(destinationRoot, ".vscode"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "Ejercicios"), Path.Combine(destinationRoot, "Ejercicios"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", ".agent"), Path.Combine(destinationRoot, "_estudio", ".agent"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", "docs"), Path.Combine(destinationRoot, "_estudio", "docs"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", "include"), Path.Combine(destinationRoot, "_estudio", "include"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", "soporte"), Path.Combine(destinationRoot, "_estudio", "soporte"));
        CopySetupScripts(workspaceRoot, destinationRoot);
    }

    private static void CopyBundledVsix(string workspaceRoot, string payloadRoot)
    {
        var candidates = new[]
        {
            VsixExtensionPaths.ResolveVsixPath(workspaceRoot),
            Path.Combine(workspaceRoot, "extension", VsixExtensionPaths.ReleasePackageFileName),
            VsixExtensionPaths.ResolvePackagedVsixPath(workspaceRoot),
        };

        var source = candidates.FirstOrDefault(File.Exists);
        if (source is null)
        {
            return;
        }

        var destination = Path.Combine(payloadRoot, VsixExtensionPaths.ReleasePackageFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyBundledRuntimeConfig(string workspaceRoot, string payloadRoot)
    {
        CopyIfExists(
            RuntimeConfigPaths.ResolveWorkspaceRuntimeConfigBootstrapPath(workspaceRoot),
            Path.Combine(payloadRoot, "runtime-config.bootstrap.json"));
    }

    private static void PromoteInstallerExecutable(string packageDirectory, string setupExe)
    {
        var installerPath = Path.Combine(packageDirectory, SetupPackageLayout.InstallerExecutableFileName);
        DeleteIfExists(installerPath);
        File.Move(setupExe, installerPath);
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyRootFile(string workspaceRoot, string packageDirectory, string fileName)
    {
        var source = Path.Combine(workspaceRoot, fileName);
        if (!File.Exists(source) || IsExcludedFile(source))
        {
            return;
        }

        var destination = Path.Combine(packageDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopySetupScripts(string workspaceRoot, string packageDirectory)
    {
        var source = Path.Combine(workspaceRoot, "_estudio", "setup");
        if (!Directory.Exists(source))
        {
            return;
        }

        var destination = Path.Combine(packageDirectory, "_estudio", "setup");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly))
        {
            if (IsExcludedFile(file))
            {
                continue;
            }

            var extension = Path.GetExtension(file);
            if (extension is not ".cmd" and not ".ps1" and not ".md")
            {
                continue;
            }

            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static string BuildChecksums(IReadOnlyList<ReleaseFileEntry> files)
    {
        return string.Join(
            Environment.NewLine,
            files.Select(file => $"{file.Sha256} *{file.Path}")) + Environment.NewLine;
    }

    private static void CopyDirectoryIfExists(string source, string destination)
    {
        if (!Directory.Exists(source) || IsExcludedDirectory(source))
        {
            return;
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly))
        {
            if (IsExcludedFile(file))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.TopDirectoryOnly))
        {
            if (IsExcludedDirectory(directory))
            {
                continue;
            }

            CopyDirectoryIfExists(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static bool IsExcludedDirectory(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".estudio-drive", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || name.Equals("runtime", StringComparison.OrdinalIgnoreCase)
            || name.Equals("usuarios", StringComparison.OrdinalIgnoreCase)
            || name.Equals("usuario", StringComparison.OrdinalIgnoreCase)
            || name.Equals("logs", StringComparison.OrdinalIgnoreCase)
            || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || name.Equals("publish", StringComparison.OrdinalIgnoreCase)
            || name.Equals("build", StringComparison.OrdinalIgnoreCase)
            || name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".tmp-state", StringComparison.OrdinalIgnoreCase)
            || name.Equals("generated", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedFile(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        return name.Equals(".estudio_usuario", StringComparison.OrdinalIgnoreCase)
            || name.Equals("runtime-config.private.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("runtime-config.bootstrap.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("gemini-key.local.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("app-config.generated.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("catalog.private.json", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".private-manifest.json", StringComparison.OrdinalIgnoreCase)
            || name.Contains("gist-state", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pyc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".vsix", StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteGeneratedDirectory(string path, string outputRoot)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        EnsureInside(path, outputRoot);
        Directory.Delete(path, recursive: true);
    }

    private static void DeleteGeneratedFile(string path, string outputRoot)
    {
        if (!File.Exists(path))
        {
            return;
        }

        EnsureInside(path, outputRoot);
        File.Delete(path);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void PruneVisibleRoot(string packageDirectory)
    {
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SetupPackageLayout.InstallerExecutableFileName,
            SetupPackageLayout.ReadmeFileName,
        };
        var allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SetupPackageLayout.PayloadDirectoryName,
        };

        foreach (var file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            if (!allowedFiles.Contains(Path.GetFileName(file)))
            {
                File.Delete(file);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(packageDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            if (!allowedDirectories.Contains(Path.GetFileName(directory)))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void EnsureInside(string path, string outputRoot)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Ruta de release fuera del directorio esperado: {fullPath}");
        }
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-');
        }

        return builder.ToString();
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string ResolvePublishedExecutableFileName(string projectPath)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        return string.IsNullOrWhiteSpace(projectName) ? DefaultPublishedExecutableFileName : projectName + ".exe";
    }

    private sealed record ReleaseManifest(
        string SetupVersion,
        string RuntimeIdentifier,
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<ReleaseFileEntry> Files);
}

public sealed record ReleasePackageRequest(
    string ProjectPath,
    string WrapperPath,
    string OutputRoot,
    string Version,
    string RuntimeIdentifier);

public sealed record ReleasePackageContext(
    string ProjectPath,
    string WrapperPath,
    string PackageDirectory,
    string Version,
    string RuntimeIdentifier,
    string Configuration);

public sealed record ReleaseFileEntry(string Path, long Bytes, string Sha256);

public sealed record ReleasePackageResult(
    string PackageDirectory,
    string ZipPath,
    string ManifestPath,
    IReadOnlyList<string> Files);
