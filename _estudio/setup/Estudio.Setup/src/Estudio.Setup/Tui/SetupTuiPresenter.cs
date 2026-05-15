namespace Estudio.Setup.Tui;

public static class SetupTuiPresenter
{
    public static SetupTuiSnapshot CreateSnapshot(SetupTuiProgressModel model, int maxLogLines = 18)
    {
        var componentLines = model.Components
            .Select(component => $"[{component.Status}] {component.Id}{FormatMessage(component.Message)}")
            .ToArray();
        var logLines = model.LogLines
            .TakeLast(Math.Max(0, maxLogLines))
            .ToArray();

        return new SetupTuiSnapshot(
            $"Progreso: {model.CompletedCount}/{model.TotalCount}",
            componentLines,
            logLines);
    }

    private static string FormatMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? string.Empty : $" - {message}";
    }
}

public sealed record SetupTuiSnapshot(
    string ProgressText,
    IReadOnlyList<string> ComponentLines,
    IReadOnlyList<string> LogLines);
