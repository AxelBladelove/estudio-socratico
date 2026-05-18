using Estudio.Setup.Security;
using Estudio.Setup.Core;

namespace Estudio.Setup.Windows;

public static class WindowsSetupExperienceMapper
{
    public static IReadOnlyList<InstallerProgressBlock> CreateBlocks()
    {
        return new[]
        {
            new InstallerProgressBlock("compiler-ready", "Preparando herramientas de programacion", "Preparando herramientas de programacion..."),
            new InstallerProgressBlock("vscode-ready", "Preparando VS Code", "Preparando VS Code..."),
            new InstallerProgressBlock("extension-ready", "Probando que todo funcione", "Instalando la extension y comprobando que VS Code quede listo..."),
            new InstallerProgressBlock("github-ready", "Preparando tu copia en GitHub", "Preparando tu copia en GitHub..."),
            new InstallerProgressBlock("workspace-ready", "Preparando tu carpeta de estudio", "Preparando tu carpeta de estudio..."),
            new InstallerProgressBlock("exercises-ready", "Preparando ejercicios", "Preparando ejercicios..."),
        };
    }

    public static void ApplyProgress(IReadOnlyList<InstallerProgressBlock> blocks, DesiredStateSetupProgressEvent progressEvent)
    {
        switch (progressEvent)
        {
            case DesiredStateNodePhaseStarted started:
            {
                var block = FindBlock(blocks, started.NodeId);
                block.HumanMessage = started.Phase == "verify"
                    ? "Probando que todo funcione..."
                    : started.HumanMessage;
                if (block.Status == SetupExecutionBlockStatus.Pending)
                {
                    block.Status = SetupExecutionBlockStatus.Pending;
                }

                break;
            }
            case DesiredStateNodePhaseFinished finished:
            {
                var block = FindBlock(blocks, finished.NodeId);
                block.HumanMessage = finished.Result.HumanMessage;
                block.TechnicalMessage = SensitiveDataRedactor.Redact(finished.Result.TechnicalMessage);
                block.Status = finished.Phase switch
                {
                    "apply" when finished.Result.IsReady => SetupExecutionBlockStatus.Applied,
                    "repair" when finished.Result.IsReady => SetupExecutionBlockStatus.Repaired,
                    _ => StatusFor(finished.Result.Status),
                };
                break;
            }
        }
    }

    public static string HumanFailureMessage(InstallerProgressBlock block)
    {
        return block.Id switch
        {
            "compiler-ready" => "No pude instalar MSYS2 automaticamente.",
            "github-ready" => "No pude terminar la conexion con GitHub.",
            "workspace-ready" => "No pude terminar de preparar tu carpeta de estudio.",
            "vscode-ready" => "No pude dejar VS Code listo automaticamente.",
            "extension-ready" => "No pude instalar la extension de VS Code automaticamente.",
            "exercises-ready" => "No pude dejar Exercism y tus ejercicios listos automaticamente.",
            _ => "No pude completar este bloque automaticamente.",
        };
    }

    public static string? GuidedHelpUrl(InstallerProgressBlock block)
    {
        if (block.Id == "exercises-ready" && block.TechnicalMessage.Contains(Steps.ExercismCTrackStep.CTrackUrl, StringComparison.OrdinalIgnoreCase))
        {
            return Steps.ExercismCTrackStep.CTrackUrl;
        }

        return GuidedSolutionCatalog.ForBlock(block.Id)?.ActionUrl;
    }

    private static InstallerProgressBlock FindBlock(IReadOnlyList<InstallerProgressBlock> blocks, string nodeId)
    {
        return blocks.First(block => string.Equals(block.Id, nodeId, StringComparison.OrdinalIgnoreCase));
    }

    private static SetupExecutionBlockStatus StatusFor(SetupNodeStatus status)
    {
        return status switch
        {
            SetupNodeStatus.Ready => SetupExecutionBlockStatus.Ready,
            SetupNodeStatus.ActionRequired => SetupExecutionBlockStatus.Pending,
            SetupNodeStatus.RepairRequired => SetupExecutionBlockStatus.Warning,
            SetupNodeStatus.Failed => SetupExecutionBlockStatus.Failed,
            _ => SetupExecutionBlockStatus.Failed,
        };
    }
}