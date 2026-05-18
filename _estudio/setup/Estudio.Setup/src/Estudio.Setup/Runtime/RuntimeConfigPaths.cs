using Estudio.Setup.Services;

namespace Estudio.Setup.Runtime;

public static class RuntimeConfigPaths
{
    public static string ResolveConfigPath(string? configRoot = null)
    {
        var root = configRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        return Path.Combine(root, "EstudioSocratico", "config.json");
    }

    public static string ResolveBundledRuntimeConfigPath(string? setupRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(setupRoot) ? AppContext.BaseDirectory : setupRoot;
        var payloadPath = Path.Combine(SetupPackageLayout.ResolvePayloadRoot(root), "runtime-config.private.json");
        if (Directory.Exists(Path.GetDirectoryName(payloadPath)!) || File.Exists(payloadPath))
        {
            return payloadPath;
        }

        return Path.Combine(root, "runtime-config.private.json");
    }

    public static string ResolveBundledRuntimeConfigBootstrapPath(string? setupRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(setupRoot) ? AppContext.BaseDirectory : setupRoot;
        var payloadPath = Path.Combine(SetupPackageLayout.ResolvePayloadRoot(root), "runtime-config.bootstrap.json");
        if (Directory.Exists(Path.GetDirectoryName(payloadPath)!) || File.Exists(payloadPath))
        {
            return payloadPath;
        }

        return Path.Combine(root, "runtime-config.bootstrap.json");
    }

    public static string ResolveWorkspaceRuntimeConfigPath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, "_estudio", "setup", "runtime-config.private.json");
    }

    public static string ResolveWorkspaceRuntimeConfigBootstrapPath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, "_estudio", "setup", "runtime-config.bootstrap.json");
    }
}
