using System.Text.Json;
using Estudio.Setup.Security;

namespace Estudio.Setup.Core;

public sealed class DesiredStateJsonProgressSink : IDesiredStateSetupProgressSink
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly TextWriter _writer;

    public DesiredStateJsonProgressSink(TextWriter writer)
    {
        _writer = writer;
    }

    public Task ReportAsync(DesiredStateSetupProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        return progressEvent switch
        {
            DesiredStateRunStarted started => WriteLineAsync(new
            {
                type = "run-started",
                engine = "desired-state",
                mode = started.Mode.ToString(),
            }, cancellationToken),
            DesiredStateNodePhaseStarted started => WriteLineAsync(new
            {
                type = "node-phase-started",
                nodeId = SensitiveDataRedactor.Redact(started.NodeId),
                nodeName = SensitiveDataRedactor.Redact(started.NodeName),
                phase = SensitiveDataRedactor.Redact(started.Phase),
                humanMessage = SensitiveDataRedactor.Redact(started.HumanMessage),
            }, cancellationToken),
            DesiredStateNodePhaseFinished finished => WriteLineAsync(new
            {
                type = "node-phase-finished",
                nodeId = SensitiveDataRedactor.Redact(finished.NodeId),
                nodeName = SensitiveDataRedactor.Redact(finished.NodeName),
                phase = SensitiveDataRedactor.Redact(finished.Phase),
                status = StatusFor(finished.Result.Status),
                success = finished.Result.IsReady,
                humanMessage = SensitiveDataRedactor.Redact(finished.Result.HumanMessage),
                technicalMessage = SensitiveDataRedactor.Redact(finished.Result.TechnicalMessage),
            }, cancellationToken),
            DesiredStateRunFinished finished => WriteLineAsync(new
            {
                type = "run-finished",
                engine = "desired-state",
                success = finished.Report.Success,
                lastSuccessfulBlock = SetupExecutionSummary.LastSuccessfulDesiredStateBlockId(finished.Report),
                nodeCount = finished.Report.Nodes.Count,
            }, cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    public Task WriteArtifactsAsync(DesiredStateSetupRunArtifacts artifacts, CancellationToken cancellationToken)
    {
        return WriteLineAsync(new
        {
            type = "artifacts",
            engine = "desired-state",
            success = artifacts.Report.Success,
            alias = SensitiveDataRedactor.Redact(artifacts.Alias),
            statePath = artifacts.StatePath,
            logPath = artifacts.LogPath,
            reportPath = artifacts.ReportPath,
        }, cancellationToken);
    }

    private async Task WriteLineAsync(object value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    private static string StatusFor(SetupNodeStatus status)
    {
        return status switch
        {
            SetupNodeStatus.Ready => "ready",
            SetupNodeStatus.ActionRequired => "pending",
            SetupNodeStatus.RepairRequired => "repair-required",
            SetupNodeStatus.Failed => "failed",
            _ => "failed",
        };
    }
}