using System.Security.Principal;
using System.Runtime.InteropServices;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class SystemProbe(AppPaths paths)
{
    public bool IsRunningElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public Dictionary<string, string> SnapshotEnvironment()
    {
        var osArch = RuntimeInformation.OSArchitecture.ToString();
        var processArch = RuntimeInformation.ProcessArchitecture.ToString();
        var isArm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64;
        var isEmulated = isArm64 && RuntimeInformation.ProcessArchitecture == Architecture.X64;
        var vmHint = DetectVirtualizationHint();
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["os"] = Environment.OSVersion.VersionString,
            ["machineName"] = Environment.MachineName,
            ["user"] = Environment.UserName,
            ["osArchitecture"] = osArch,
            ["processArchitecture"] = processArch,
            ["environmentKind"] = isArm64
                ? isEmulated ? "Windows ARM64 en emulacion x64" : "Windows ARM64"
                : RuntimeInformation.OSArchitecture == Architecture.X64 ? "Windows x64" : $"Windows {osArch}",
            ["virtualizationHint"] = vmHint,
            ["arm64EmulationWarning"] = isEmulated
                ? "Proceso x64 sobre Windows ARM64; si una herramienta cae por emulacion, revisar instalador ARM64 nativo."
                : "",
            ["is64BitOs"] = Environment.Is64BitOperatingSystem.ToString(),
            ["isElevated"] = IsRunningElevated().ToString(),
            ["localAppData"] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ["userProfile"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["repoRoot"] = paths.RepoRoot ?? "",
            ["path"] = Environment.GetEnvironmentVariable("PATH") ?? ""
        };
    }

    private static string DetectVirtualizationHint()
    {
        var text = $"{Environment.MachineName} {RuntimeInformation.OSDescription}";
        if (text.Contains("Parallels", StringComparison.OrdinalIgnoreCase))
        {
            return "Parallels/VM";
        }

        var manufacturer = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "";
        if (manufacturer.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            manufacturer.Contains("VMware", StringComparison.OrdinalIgnoreCase))
        {
            return "VM";
        }

        return "";
    }
}

public sealed class PathManager(AppPaths paths, LogManager logManager)
{
    public IReadOnlyList<string> GetPathEntries()
    {
        var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ??
                   Environment.GetEnvironmentVariable("PATH") ??
                   "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<bool> EnsureUserPathEntryAsync(string entry, ManifestManager manifestManager, CancellationToken cancellationToken)
    {
        var normalized = Path.GetFullPath(entry).TrimEnd(Path.DirectorySeparatorChar);
        var before = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var parts = before.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (parts.Any(x => string.Equals(Path.GetFullPath(x).TrimEnd(Path.DirectorySeparatorChar), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        parts.Add(normalized);
        var after = string.Join(Path.PathSeparator, parts);
        Environment.SetEnvironmentVariable("PATH", after, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("PATH", normalized + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? ""));

        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        manifest.PathChanges.Add(new PathChange { Scope = "User", Before = before, After = after });
        await manifestManager.SaveAsync(manifest, cancellationToken).ConfigureAwait(false);
        await logManager.WriteAsync("info", "path", $"PATH de usuario actualizado con {normalized}", cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public string GetManagedToolsPath()
    {
        var tools = Path.Combine(paths.ToolsRoot, "bin");
        Directory.CreateDirectory(tools);
        return tools;
    }
}

public sealed class EnvironmentManager(PathManager pathManager)
{
    public IReadOnlyList<string> CurrentPathEntries() => pathManager.GetPathEntries();
}

public sealed class SecurityManager
{
    public string Redact(string? text) => SecretRedactor.Redact(text);

    public void RequireSafeDeleteRoot(string root, string target)
    {
        PathSafety.RequireInside(root, target, "delete");
    }
}
