using Estudio.Setup.Services;

namespace Estudio.Setup.Core;

public sealed class ExtensionReadyNode : StepBackedSetupStateNode
{
    public ExtensionReadyNode(ICommandRunner commandRunner, string workspaceRoot)
        : this(new ISetupStep[]
        {
            CreateVsixPackageStep(workspaceRoot, commandRunner),
            CreateVsixExtensionStep(workspaceRoot, commandRunner),
        })
    {
    }

    public ExtensionReadyNode(IEnumerable<ISetupStep> steps)
        : base("extension-ready", "la extension de VS Code", steps)
    {
    }

    protected override string ReadyHumanMessage => "La extension de VS Code ya esta lista.";
    protected override string PendingHumanMessage => "Voy a instalar la extension de VS Code.";
    protected override string RepairHumanMessage => "Voy a reparar la extension de VS Code.";
    protected override string AppliedHumanMessage => "La extension de VS Code quedo instalada.";
    protected override string RepairedHumanMessage => "La extension de VS Code fue reparada.";
}