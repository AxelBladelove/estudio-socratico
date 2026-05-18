using Estudio.Setup.Services;

namespace Estudio.Setup.Core;

public static class DefaultSetupStateNodes
{
    public static IReadOnlyList<ISetupStateNode> Create(
        ICommandRunner commandRunner,
        string studentAlias,
        string workspaceRoot,
        string? appDataRoot = null)
    {
        return new ISetupStateNode[]
        {
            new GitHubReadyNode(commandRunner, studentAlias),
            new WorkspaceReadyNode(commandRunner, studentAlias, workspaceRoot),
            new VSCodeReadyNode(commandRunner, studentAlias, appDataRoot),
            new ExtensionReadyNode(commandRunner, workspaceRoot),
            new CompilerReadyNode(commandRunner),
            new ExercisesReadyNode(commandRunner, workspaceRoot, appDataRoot),
        };
    }
}