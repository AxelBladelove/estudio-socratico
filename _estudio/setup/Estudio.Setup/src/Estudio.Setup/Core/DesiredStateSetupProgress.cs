namespace Estudio.Setup.Core;

public interface IDesiredStateSetupProgressSink
{
    Task ReportAsync(DesiredStateSetupProgressEvent progressEvent, CancellationToken cancellationToken);
}

public abstract record DesiredStateSetupProgressEvent;

public sealed record DesiredStateRunStarted(SetupMode Mode) : DesiredStateSetupProgressEvent;

public sealed record DesiredStateNodePhaseStarted(
    string NodeId,
    string NodeName,
    string Phase,
    string HumanMessage) : DesiredStateSetupProgressEvent;

public sealed record DesiredStateNodePhaseFinished(
    string NodeId,
    string NodeName,
    string Phase,
    SetupNodeResult Result) : DesiredStateSetupProgressEvent;

public sealed record DesiredStateRunFinished(DesiredStateSetupReport Report) : DesiredStateSetupProgressEvent;

public sealed class NullDesiredStateSetupProgressSink : IDesiredStateSetupProgressSink
{
    public static NullDesiredStateSetupProgressSink Instance { get; } = new();

    private NullDesiredStateSetupProgressSink()
    {
    }

    public Task ReportAsync(DesiredStateSetupProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}