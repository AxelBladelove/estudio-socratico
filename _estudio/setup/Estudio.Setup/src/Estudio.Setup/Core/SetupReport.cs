namespace Estudio.Setup.Core;

public sealed record SetupReport(
    bool Success,
    string LastSuccessfulStep,
    IReadOnlyList<StepExecution> Steps)
{
    public static SetupReport Passed(IReadOnlyList<StepExecution> steps)
    {
        return new SetupReport(true, "verify-final", steps);
    }

    public static SetupReport Failed(string lastSuccessfulStep, IReadOnlyList<StepExecution> steps)
    {
        return new SetupReport(false, lastSuccessfulStep, steps);
    }
}

public sealed record StepExecution(string StepId, string Phase, StepResult Result);
