using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Estudio.Setup.Services;
using Estudio.Setup.State;

namespace Estudio.Setup.Release;

public sealed class ReleasePackager
{
    private readonly Func<ReleasePackageContext, CancellationToken, Task> _publishAsync;

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
        })
    {
    }

    public ReleasePackager(Func<ReleasePackageContext, CancellationToken, Task> publishAsync)
    {
        _publishAsync = publishAsync;
    }

    public async Task<ReleasePackageResult> CreateAsync(
        ReleasePackageRequest request,
        CancellationToken cancellationToken)
    {
        var outputRoot = Path.GetFullPath(request.OutputRoot);
        Directory.CreateDirectory(outputRoot);
        var packageName = $"estudio-socratico-setup-{Sanitize(request.Version)}-{request.RuntimeIdentifier}";
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

        var setupExe = Path.Combine(packageDirectory, "Estudio.Setup.exe");
        if (!File.Exists(setupExe))
        {
            throw new InvalidOperationException($"dotnet publish no genero {setupExe}.");
        }

        RemoveDebugSymbols(packageDirectory);
        File.Copy(context.WrapperPath, Path.Combine(packageDirectory, "Estudio.Setup.cmd"), overwrite: true);
        await File.WriteAllTextAsync(
            Path.Combine(packageDirectory, "README.txt"),
            BuildReadme(request.Version),
            cancellationToken);

        var files = BuildFileEntries(packageDirectory);
        var manifestPath = Path.Combine(packageDirectory, "release-manifest.json");
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new ReleaseManifest(
                    request.Version,
                    request.RuntimeIdentifier,
                    DateTimeOffset.UtcNow,
                    files),
                new JsonSerializerOptions { WriteIndented = true },
                cancellationToken);
        }

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
            ProjectPath: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "src", "Estudio.Setup", "Estudio.Setup.csproj"),
            WrapperPath: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup.cmd"),
            OutputRoot: Path.Combine(workspaceRoot, "_estudio", "setup", "Estudio.Setup", "publish", "release"),
            Version: version,
            RuntimeIdentifier: runtimeIdentifier);
    }

    private static IReadOnlyList<ReleaseFileEntry> BuildFileEntries(string packageDirectory)
    {
        return Directory
            .EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new ReleaseFileEntry(
                Path.GetRelativePath(packageDirectory, path).Replace('\\', '/'),
                new FileInfo(path).Length,
                Sha256(path)))
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

            Ejecuta Estudio.Setup.cmd install --tui para instalacion visual.
            Ejecuta Estudio.Setup.cmd verify para diagnostico no destructivo.
            """;
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
