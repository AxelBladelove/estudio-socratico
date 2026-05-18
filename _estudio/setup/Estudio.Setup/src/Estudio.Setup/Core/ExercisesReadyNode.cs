using Estudio.Setup.Runtime;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Core;

public sealed class ExercisesReadyNode : StepBackedSetupStateNode
{
    public ExercisesReadyNode(
        ICommandRunner commandRunner,
        string workspaceRoot,
        string? appDataRoot = null,
        IGeminiRuntimeConfigProvider? geminiRuntimeConfigProvider = null)
        : this(CreateSteps(commandRunner, workspaceRoot, appDataRoot, geminiRuntimeConfigProvider))
    {
    }

    public ExercisesReadyNode(IEnumerable<ISetupStep> steps)
        : base("exercises-ready", "tus ejercicios", steps)
    {
    }

    protected override string ReadyHumanMessage => "Tus ejercicios ya estan listos para empezar.";
    protected override string PendingHumanMessage => "Voy a preparar tus ejercicios y el acceso a Exercism.";
    protected override string RepairHumanMessage => "Voy a reparar la preparacion de tus ejercicios.";
    protected override string AppliedHumanMessage => "Tus ejercicios quedaron preparados.";
    protected override string RepairedHumanMessage => "La preparacion de tus ejercicios fue reparada.";

    private static IReadOnlyList<ISetupStep> CreateSteps(
        ICommandRunner commandRunner,
        string workspaceRoot,
        string? appDataRoot,
        IGeminiRuntimeConfigProvider? geminiRuntimeConfigProvider)
    {
        geminiRuntimeConfigProvider ??= new CompositeGeminiRuntimeConfigProvider(
            new FileGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveBundledRuntimeConfigPath()),
            new BootstrapGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveBundledRuntimeConfigBootstrapPath()),
            new FileGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveWorkspaceRuntimeConfigPath(workspaceRoot)),
            new BootstrapGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveWorkspaceRuntimeConfigBootstrapPath(workspaceRoot)),
            new EnvironmentGeminiRuntimeConfigProvider());

        return new ISetupStep[]
        {
            new WingetPackageStep("exercism-cli", "Exercism CLI", "Exercism.CLI", "exercism", "version", commandRunner),
            new ExercismCTrackStep(commandRunner),
            new GeminiRuntimeConfigStep(RuntimeConfigPaths.ResolveConfigPath(appDataRoot), geminiRuntimeConfigProvider),
            new ExerciseCatalogStep(workspaceRoot),
        };
    }
}