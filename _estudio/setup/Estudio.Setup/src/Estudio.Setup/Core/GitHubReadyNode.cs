using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Core;

public sealed class GitHubReadyNode : StepBackedSetupStateNode
{
    public GitHubReadyNode(ICommandRunner commandRunner, string studentAlias)
        : this(new ISetupStep[]
        {
            CreateGitHubCliStep(commandRunner),
            new GitHubAuthStep(commandRunner),
            new GitHubForkStep(commandRunner, studentAlias),
        })
    {
    }

    public GitHubReadyNode(IEnumerable<ISetupStep> steps)
        : base("github-ready", "tu copia en GitHub", steps)
    {
    }

    protected override string ReadyHumanMessage => "Tu copia en GitHub ya esta lista.";
    protected override string PendingHumanMessage => "Voy a preparar tu copia en GitHub.";
    protected override string RepairHumanMessage => "Voy a reparar tu conexion con GitHub.";
    protected override string AppliedHumanMessage => "Tu copia en GitHub quedo preparada.";
    protected override string RepairedHumanMessage => "La conexion con GitHub fue reparada.";
}