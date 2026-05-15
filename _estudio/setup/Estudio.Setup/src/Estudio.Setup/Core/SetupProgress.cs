namespace Estudio.Setup.Core;

public interface ISetupProgressSink
{
    Task ReportAsync(SetupProgressEvent progressEvent, CancellationToken cancellationToken);
}

public abstract record SetupProgressEvent;

public sealed record SetupRunStarted(SetupMode Mode) : SetupProgressEvent;

public sealed record SetupPhaseStarted(string StepId, string StepName, string Phase) : SetupProgressEvent;

public sealed record SetupPhaseFinished(StepExecution Execution) : SetupProgressEvent;

public sealed record SetupRunFinished(SetupReport Report) : SetupProgressEvent;

public sealed class NullSetupProgressSink : ISetupProgressSink
{
    public static NullSetupProgressSink Instance { get; } = new();

    private NullSetupProgressSink()
    {
    }

    public Task ReportAsync(SetupProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
