using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Core;

public sealed class WorkspaceReadyNode : StepBackedSetupStateNode
{
    public WorkspaceReadyNode(ICommandRunner commandRunner, string studentAlias, string workspaceRoot)
        : this(new ISetupStep[]
        {
            CreateGitReadyStep(commandRunner),
            new GitWorkspaceStep(commandRunner, studentAlias, workspaceRoot),
            new GitIdentityStep(commandRunner, studentAlias, workspaceRoot),
            new GitSafetyBackupStep(commandRunner, workspaceRoot),
            new GitHubAliasRenameStep(commandRunner, workspaceRoot, studentAlias),
            new LocalAliasStep(workspaceRoot, studentAlias),
            new GitRemoteStep(commandRunner, studentAlias, workspaceRoot),
            new GitProjectUpdateStep(commandRunner, workspaceRoot),
        })
    {
    }

    public WorkspaceReadyNode(IEnumerable<ISetupStep> steps)
        : base("workspace-ready", "tu carpeta de estudio", steps)
    {
    }

    protected override string ReadyHumanMessage => "Tu carpeta de estudio ya esta lista.";
    protected override string PendingHumanMessage => "Voy a preparar tu carpeta de estudio.";
    protected override string RepairHumanMessage => "Voy a reparar tu carpeta de estudio.";
    protected override string AppliedHumanMessage => "Tu carpeta de estudio quedo preparada.";
    protected override string RepairedHumanMessage => "Tu carpeta de estudio fue reparada.";
}