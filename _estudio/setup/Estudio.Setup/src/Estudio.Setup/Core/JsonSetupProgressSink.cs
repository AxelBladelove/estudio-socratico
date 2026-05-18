using System.Text.Json;
using Estudio.Setup.Security;

namespace Estudio.Setup.Core;

public sealed class JsonSetupProgressSink : ISetupProgressSink
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly TextWriter _writer;

    public JsonSetupProgressSink(TextWriter writer)
    {
        _writer = writer;
    }

    public Task ReportAsync(SetupProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        return progressEvent switch
        {
            SetupRunStarted started => WriteLineAsync(new
            {
                type = "run-started",
                mode = started.Mode.ToString(),
            }, cancellationToken),
            SetupPhaseStarted started => WriteLineAsync(new
            {
                type = "phase-started",
                stepId = SensitiveDataRedactor.Redact(started.StepId),
                stepName = SensitiveDataRedactor.Redact(started.StepName),
                phase = SensitiveDataRedactor.Redact(started.Phase),
            }, cancellationToken),
            SetupPhaseFinished finished => WriteLineAsync(new
            {
                type = "phase-finished",
                stepId = SensitiveDataRedactor.Redact(finished.Execution.StepId),
                phase = SensitiveDataRedactor.Redact(finished.Execution.Phase),
                status = StatusFor(finished.Execution.Result),
                success = finished.Execution.Result.Success,
                missing = finished.Execution.Result.IsMissing,
                warning = finished.Execution.Result.IsWarning,
                message = SensitiveDataRedactor.Redact(finished.Execution.Result.Message),
            }, cancellationToken),
            SetupRunFinished finished => WriteLineAsync(new
            {
                type = "run-finished",
                success = finished.Report.Success,
                lastSuccessfulStep = finished.Report.LastSuccessfulStep,
                stepCount = finished.Report.Steps.Count,
            }, cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    public Task WriteArtifactsAsync(SetupRunArtifacts artifacts, CancellationToken cancellationToken)
    {
        return WriteLineAsync(new
        {
            type = "artifacts",
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

    private static string StatusFor(StepResult result)
    {
        if (result.IsWarning)
        {
            return "warning";
        }

        if (result.Success)
        {
            return "ok";
        }

        return result.IsMissing ? "missing" : "error";
    }
}
