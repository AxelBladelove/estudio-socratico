using Estudio.Setup.Security;

namespace Estudio.Setup.Core;

public static class SetupConsolePresenter
{
    public static async Task WriteAsync(
        TextWriter writer,
        SetupOptions options,
        SetupExecutionArtifacts artifacts,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync($"Estudio.Setup 2.0");
        await writer.WriteLineAsync($"Modo: {options.Mode}");
        await writer.WriteLineAsync($"Engine: {EngineLabel(artifacts.Engine)}");
        await writer.WriteLineAsync($"Alias: {SensitiveDataRedactor.Redact(artifacts.Alias)}");
        await writer.WriteLineAsync($"Estado: {artifacts.StatePath}");
        await writer.WriteLineAsync($"Log: {artifacts.LogPath}");
        await writer.WriteLineAsync($"Reporte: {artifacts.ReportPath}");

        foreach (var block in artifacts.Blocks)
        {
            await writer.WriteLineAsync($"{MarkerFor(block.Status)} {SensitiveDataRedactor.Redact(block.Name)}: {SensitiveDataRedactor.Redact(block.HumanMessage)}");
        }

        await writer.WriteLineAsync(artifacts.Success ? "Resultado: OK" : "Resultado: ERROR");
        await writer.FlushAsync(cancellationToken);
    }

    private static string EngineLabel(SetupExecutionEngine engine)
    {
        return engine == SetupExecutionEngine.DesiredState ? "desired-state" : "legacy";
    }

    private static string MarkerFor(SetupExecutionBlockStatus status)
    {
        return status switch
        {
            SetupExecutionBlockStatus.Ready => "LISTO",
            SetupExecutionBlockStatus.Applied => "APLICADO",
            SetupExecutionBlockStatus.Repaired => "REPARADO",
            SetupExecutionBlockStatus.Pending => "PENDIENTE",
            SetupExecutionBlockStatus.Warning => "ADVERTENCIA",
            SetupExecutionBlockStatus.Failed => "ERROR",
            _ => "INFO",
        };
    }
}