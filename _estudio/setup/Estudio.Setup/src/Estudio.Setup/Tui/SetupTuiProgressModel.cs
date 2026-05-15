using Estudio.Setup.Core;

namespace Estudio.Setup.Tui;

public sealed class SetupTuiProgressModel : ISetupProgressSink
{
    private readonly List<SetupTuiComponent> _components;
    private readonly List<string> _logLines = new();

    public SetupTuiProgressModel(IEnumerable<string> stepIds)
    {
        _components = stepIds
            .Select(stepId => new SetupTuiComponent(stepId, "PENDIENTE", string.Empty))
            .ToList();
    }

    public IReadOnlyList<SetupTuiComponent> Components => _components;
    public IReadOnlyList<string> LogLines => _logLines;
    public int CompletedCount => _components.Count(component => component.Status is "OK" or "ADVERTENCIA");
    public int TotalCount => _components.Count;

    public Task ReportAsync(SetupProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        switch (progressEvent)
        {
            case SetupRunStarted started:
                _logLines.Add($"Modo: {started.Mode}");
                break;
            case SetupPhaseStarted started:
                SetStatus(started.StepId, "EN CURSO", $"{started.Phase}...");
                _logLines.Add($"{started.StepId}.{started.Phase}: iniciando");
                break;
            case SetupPhaseFinished finished:
                ApplyExecution(finished.Execution);
                break;
            case SetupRunFinished finished:
                _logLines.Add(finished.Report.Success ? "Resultado: OK" : "Resultado: ERROR");
                break;
        }

        return Task.CompletedTask;
    }

    public void ApplyExecution(StepExecution execution)
    {
        SetStatus(execution.StepId, StatusFor(execution.Result), execution.Result.Message);
        _logLines.Add($"{execution.StepId}.{execution.Phase}: {execution.Result.Message}");
    }

    public IReadOnlyList<string> FailedStepIds()
    {
        return _components
            .Where(component => component.Status is "FALTA" or "ERROR")
            .Select(component => component.Id)
            .ToArray();
    }

    private void SetStatus(string stepId, string status, string message)
    {
        var index = _components.FindIndex(component => string.Equals(component.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _components.Add(new SetupTuiComponent(stepId, status, message));
            return;
        }

        _components[index] = _components[index] with
        {
            Status = status,
            Message = message,
        };
    }

    private static string StatusFor(StepResult result)
    {
        if (result.IsWarning)
        {
            return "ADVERTENCIA";
        }

        if (result.Success)
        {
            return "OK";
        }

        return result.IsMissing ? "FALTA" : "ERROR";
    }
}

public sealed record SetupTuiComponent(string Id, string Status, string Message);
