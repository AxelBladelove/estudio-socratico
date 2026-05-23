using System.Diagnostics;
using System.Reflection;

namespace EstudioSocratico.Configurator.Core;

public sealed record RuntimeVersionInfo
{
    public string PublicDisplayVersion { get; init; } = ProductInfo.PublicDisplayVersion;
    public string InternalPackageVersion { get; init; } = ProductInfo.Version;
    public string InstalledBuild { get; init; } = ProductInfo.Version;
    public string Source { get; init; } = "runtime";
}

public static class ProductInfo
{
    public const string PublicDisplayVersion = "2.0";
    public const string Version = "2.0.15";
    public const string SetupFileName = "Estudio-Socratico-Setup-v2.0.15-x64.exe";
    public const string DisplayName = "Estudio Socratico Instalador";
    public const string CompanyName = "Estudio Socratico";
    public const string AppDataFolderName = "EstudioSocratico";
    public const string ManifestFileName = "installer-manifest.json";
    public const string LogsFolderName = "Logs";
    public const string BaseRepository = "AxelBladelove/estudio-socratico";
    public const string BaseRepositoryOwner = "AxelBladelove";
    public const string RepositoryName = "estudio-socratico";
    public const string VSCodeExtensionId = "estudio-socratico.estudio-exercism";
    public const string DefaultWorkspaceFolderPrefix = "Estudio-Socratico";
    public const string DefaultWorkspaceFolderName = "estudio-socratico";
    public const string DefaultMsys2Root = @"C:\msys64";
    public const string DefaultMsys2UcrtBin = @"C:\msys64\ucrt64\bin";

    public static RuntimeVersionInfo GetRuntimeVersionInfo()
    {
        var assembly = typeof(ProductInfo).Assembly;
        var location = assembly.Location;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string? fileVersion = null;
        string? productVersion = null;

        if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(location);
            fileVersion = versionInfo.FileVersion;
            productVersion = versionInfo.ProductVersion;
        }

        return new RuntimeVersionInfo
        {
            InstalledBuild = FirstNonEmpty(informationalVersion, productVersion, fileVersion, Version),
            Source = DetectRuntimeSource(location)
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? Version;

    private static string DetectRuntimeSource(string? assemblyLocation)
    {
        var fullPath = string.IsNullOrWhiteSpace(assemblyLocation)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(assemblyLocation);

        if (fullPath.Contains(@"\Program Files\", StringComparison.OrdinalIgnoreCase) ||
            fullPath.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase))
        {
            return "installed app";
        }

        var current = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(Path.Combine(current, ".git")))
            {
                return "local repo";
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) ||
                string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        return "runtime";
    }
}
