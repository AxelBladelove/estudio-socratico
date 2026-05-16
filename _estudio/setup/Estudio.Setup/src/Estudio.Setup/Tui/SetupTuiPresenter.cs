namespace Estudio.Setup.Tui;

public static class SetupTuiPresenter
{
    public static SetupTuiSnapshot CreateSnapshot(SetupTuiProgressModel model, int maxLogLines = 18)
    {
        var componentLines = model.Components
            .Select(FormatComponent)
            .ToArray();
        var logLines = model.LogLines
            .TakeLast(Math.Max(0, maxLogLines))
            .ToArray();

        return new SetupTuiSnapshot(
            FormatProgress(model),
            componentLines,
            logLines);
    }

    private static string FormatProgress(SetupTuiProgressModel model)
    {
        var okCount = model.Components.Count(component => component.Status == "OK");
        var warningCount = model.Components.Count(component => component.Status == "ADVERTENCIA");
        var failureCount = model.Components.Count(component => component.Status is "FALTA" or "ERROR");

        return $"Progreso {model.CompletedCount}/{model.TotalCount} | OK {okCount} | Avisos {warningCount} | Fallos {failureCount}";
    }

    private static string FormatComponent(SetupTuiComponent component)
    {
        var id = component.Id.Length > 29 ? component.Id[..29] : component.Id;
        return $"{BadgeFor(component.Status).PadRight(6)}{id.PadRight(30)}{component.Message}".TrimEnd();
    }

    private static string BadgeFor(string status)
    {
        return status switch
        {
            "OK" => "OK",
            "ADVERTENCIA" => "WARN",
            "FALTA" => "MISS",
            "ERROR" => "FAIL",
            "EN CURSO" => "RUN",
            _ => "WAIT",
        };
    }
}

public sealed record SetupTuiSnapshot(
    string ProgressText,
    IReadOnlyList<string> ComponentLines,
    IReadOnlyList<string> LogLines);
