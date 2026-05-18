using Estudio.Setup.Services;

namespace Estudio.Setup.Core;

public sealed class CompilerReadyNode : StepBackedSetupStateNode
{
    public CompilerReadyNode(ICommandRunner commandRunner)
        : this(new ISetupStep[]
        {
            CreateCompilerToolchainStep(commandRunner),
            CreateCompilerPathStep(),
        })
    {
    }

    public CompilerReadyNode(IEnumerable<ISetupStep> steps)
        : base("compiler-ready", "el compilador de C", steps)
    {
    }

    protected override string ReadyHumanMessage => "El compilador de C ya esta listo.";
    protected override string PendingHumanMessage => "Voy a preparar el compilador de C.";
    protected override string RepairHumanMessage => "Voy a reparar el compilador de C.";
    protected override string AppliedHumanMessage => "El compilador de C quedo preparado.";
    protected override string RepairedHumanMessage => "El compilador de C fue reparado.";
}