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
        return Path.Combine(root, "runtime-config.private.json");
    }

    public static string ResolveBundledRuntimeConfigBootstrapPath(string? setupRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(setupRoot) ? AppContext.BaseDirectory : setupRoot;
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
