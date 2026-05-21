using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class AppPaths
{
    public AppPaths(string? repoRoot = null, string? localAppDataRoot = null)
    {
        UserProfileRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LocalAppDataRoot = localAppDataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductInfo.AppDataFolderName);
        LogsRoot = Path.Combine(LocalAppDataRoot, ProductInfo.LogsFolderName);
        DownloadCache = Path.Combine(LocalAppDataRoot, "Downloads");
        ToolsRoot = Path.Combine(LocalAppDataRoot, "Tools");
        ManifestPath = Path.Combine(LocalAppDataRoot, ProductInfo.ManifestFileName);
        RepoRoot = repoRoot ?? TryResolveRepoRoot(Environment.CurrentDirectory);
        DefaultWorkspacePath = GetRecommendedWorkspacePath(Environment.UserName);
    }

    public string UserProfileRoot { get; }
    public string LocalAppDataRoot { get; }
    public string LogsRoot { get; }
    public string DownloadCache { get; }
    public string ToolsRoot { get; }
    public string ManifestPath { get; }
    public string? RepoRoot { get; }
    public string DefaultWorkspacePath { get; }

    public string GetRecommendedWorkspacePath(string? localAlias)
    {
        var alias = LocalAliasNormalizer.Normalize(localAlias, Environment.UserName);
        return Path.Combine(UserProfileRoot, $"{ProductInfo.DefaultWorkspaceFolderPrefix}-{alias}");
    }

    public void EnsureBaseDirectories()
    {
        Directory.CreateDirectory(LocalAppDataRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(DownloadCache);
        Directory.CreateDirectory(ToolsRoot);
    }

    public static string? TryResolveRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "_estudio")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
