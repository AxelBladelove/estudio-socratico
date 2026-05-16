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
    private readonly Func<ReleasePackageContext, CancellationToken, Task> _buildTextualAsync;

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
        async (context, cancellationToken) =>
        {
            var appPath = ResolveTextualAppPath(context.ProjectPath);
            var requirementsPath = Path.Combine(Path.GetDirectoryName(appPath)!, "requirements.txt");
            var installResult = await commandRunner.RunAsync(
                "py",
                $"-3.10 -m pip install -r {Quote(requirementsPath)}",
                cancellationToken);
            if (!installResult.WasStarted)
            {
                throw new InvalidOperationException("No se pudo ejecutar python para preparar el instalador Textual.");
            }

            if (!installResult.IsSuccess)
            {
                throw new InvalidOperationException($"pip install fallo. {FirstNonEmpty(installResult.StandardError, installResult.StandardOutput)}");
            }

            var buildRoot = Path.Combine(Path.GetDirectoryName(appPath)!, "build");
            var arguments = string.Join(
                " ",
                "-3.10",
                "-m",
                "PyInstaller",
                "--noconfirm",
                "--clean",
                "--onefile",
                "--name",
                "Estudio.Setup.Textual",
                "--distpath",
                Quote(context.PackageDirectory),
                "--workpath",
                Quote(Path.Combine(buildRoot, "work")),
                "--specpath",
                Quote(Path.Combine(buildRoot, "spec")),
                "--collect-all",
                "textual",
                Quote(appPath));
            var result = await commandRunner.RunAsync("py", arguments, cancellationToken);
            if (!result.WasStarted)
            {
                throw new InvalidOperationException("No se pudo ejecutar python para empaquetar el instalador Textual.");
            }

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"PyInstaller fallo. {FirstNonEmpty(result.StandardError, result.StandardOutput)}");
            }
        })
    {
    }

    public ReleasePackager(Func<ReleasePackageContext, CancellationToken, Task> publishAsync)
        : this(publishAsync, (_, _) => Task.CompletedTask)
    {
    }

    public ReleasePackager(
        Func<ReleasePackageContext, CancellationToken, Task> publishAsync,
        Func<ReleasePackageContext, CancellationToken, Task> buildTextualAsync)
    {
        _publishAsync = publishAsync;
        _buildTextualAsync = buildTextualAsync;
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

        await _buildTextualAsync(context, cancellationToken);
        var textualExe = Path.Combine(packageDirectory, "Estudio.Setup.Textual.exe");
        if (!File.Exists(textualExe))
        {
            throw new InvalidOperationException($"El empaquetado Textual no genero {textualExe}.");
        }

        CopyWorkspacePayload(ResolveWorkspaceRoot(request), packageDirectory);
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

            Doble clic en Estudio.Setup.cmd abre el instalador Textual.
            Ejecuta Estudio.Setup.cmd install --tui para instalacion visual.
            Ejecuta Estudio.Setup.cmd reinstall --tui para reinstalar integraciones locales.
            Ejecuta Estudio.Setup.cmd uninstall para desinstalar integraciones locales.
            Ejecuta Estudio.Setup.cmd verify para diagnostico no destructivo.
            """;
    }

    private static string ResolveTextualAppPath(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? throw new InvalidOperationException("No se pudo resolver el proyecto Estudio.Setup.");
        return Path.GetFullPath(Path.Combine(projectDirectory, "..", "..", "..", "textual", "setup_textual_app.py"));
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

    private static void CopyWorkspacePayload(string workspaceRoot, string packageDirectory)
    {
        CopyRootFile(workspaceRoot, packageDirectory, "README.md");
        CopyRootFile(workspaceRoot, packageDirectory, "AGENTS.md");
        CopyRootFile(workspaceRoot, packageDirectory, "package.json");
        CopyRootFile(workspaceRoot, packageDirectory, ".gitignore");
        CopyRootFile(workspaceRoot, packageDirectory, "Instalar Estudio Socratico.cmd");
        CopyRootFile(workspaceRoot, packageDirectory, "Actualizar Estudio Socratico.cmd");
        CopyRootFile(workspaceRoot, packageDirectory, "Reinstalar Estudio Socratico.cmd");
        CopyRootFile(workspaceRoot, packageDirectory, "Desinstalar Estudio Socratico.cmd");

        CopyDirectoryIfExists(Path.Combine(workspaceRoot, ".vscode"), Path.Combine(packageDirectory, ".vscode"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "Ejercicios"), Path.Combine(packageDirectory, "Ejercicios"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", ".agent"), Path.Combine(packageDirectory, "_estudio", ".agent"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", "docs"), Path.Combine(packageDirectory, "_estudio", "docs"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", "include"), Path.Combine(packageDirectory, "_estudio", "include"));
        CopyDirectoryIfExists(Path.Combine(workspaceRoot, "_estudio", "soporte"), Path.Combine(packageDirectory, "_estudio", "soporte"));
        CopySetupScripts(workspaceRoot, packageDirectory);
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
