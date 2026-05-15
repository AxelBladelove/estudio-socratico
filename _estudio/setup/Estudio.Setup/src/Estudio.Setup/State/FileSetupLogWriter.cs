using System.Text;
using Estudio.Setup.Core;

namespace Estudio.Setup.State;

public sealed class FileSetupLogWriter
{
    private readonly string _logRoot;
    private readonly Func<DateTimeOffset> _clock;

    public FileSetupLogWriter(string logRoot)
        : this(logRoot, () => DateTimeOffset.Now)
    {
    }

    public FileSetupLogWriter(string logRoot, Func<DateTimeOffset> clock)
    {
        _logRoot = logRoot;
        _clock = clock;
    }

    public async Task<string> SaveAsync(
        SetupOptions options,
        string studentAlias,
        SetupReport report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_logRoot);
        var now = _clock();
        var path = Path.Combine(_logRoot, $"setup-{now:yyyy-MM-dd}.log");

        var builder = new StringBuilder();
        builder.AppendLine("============================================================");
        builder.AppendLine($"Fecha: {now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine("Estudio.Setup 2.0");
        builder.AppendLine($"Modo: {options.Mode}");
        builder.AppendLine($"Alias: {studentAlias}");
        builder.AppendLine($"Ultimo paso exitoso: {report.LastSuccessfulStep}");

        foreach (var step in report.Steps)
        {
            builder.AppendLine($"{MarkerFor(step.Result)} {step.StepId}.{step.Phase}: {step.Result.Message}");
        }

        builder.AppendLine(report.Success ? "Resultado: OK" : "Resultado: ERROR");
        builder.AppendLine();

        await File.AppendAllTextAsync(path, builder.ToString(), cancellationToken);
        return path;
    }

    private static string MarkerFor(StepResult result)
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
