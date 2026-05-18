using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Core;

public sealed class VSCodeReadyNode : StepBackedSetupStateNode
{
    public VSCodeReadyNode(ICommandRunner commandRunner, string studentAlias, string? appDataRoot = null)
        : this(new ISetupStep[]
        {
            new WingetPackageStep("node", "Node.js", "OpenJS.NodeJS.LTS", "node", "--version", commandRunner),
            CreateVsCodeStep(commandRunner),
            new WingetPackageStep("powershell7", "PowerShell 7", "Microsoft.PowerShell", "pwsh", "--version", commandRunner),
            CreateVsCodeSettingsStep(studentAlias, appDataRoot),
        })
    {
    }

    public VSCodeReadyNode(IEnumerable<ISetupStep> steps)
        : base("vscode-ready", "VS Code", steps)
    {
    }

    protected override string ReadyHumanMessage => "VS Code ya esta listo para estudiar.";
    protected override string PendingHumanMessage => "Voy a preparar VS Code.";
    protected override string RepairHumanMessage => "Voy a reparar la configuracion de VS Code.";
    protected override string AppliedHumanMessage => "VS Code quedo preparado.";
    protected override string RepairedHumanMessage => "VS Code fue reparado.";
}