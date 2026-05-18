namespace Estudio.Setup.Services;

public interface IVsCodeWorkspaceLauncher
{
    void OpenWorkspace(string workspaceRoot, string studentAlias, string? exercismToken = null);
}