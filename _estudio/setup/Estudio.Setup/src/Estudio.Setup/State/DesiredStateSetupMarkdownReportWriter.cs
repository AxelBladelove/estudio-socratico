using System.Text;
using Estudio.Setup.Core;
using Estudio.Setup.Security;

namespace Estudio.Setup.State;

public sealed class DesiredStateSetupMarkdownReportWriter
{
    private readonly string _reportRoot;

    public DesiredStateSetupMarkdownReportWriter(string reportRoot)
    {
        _reportRoot = reportRoot;
    }

    public async Task<string> SaveAsync(
        SetupOptions options,
        string studentAlias,
        DesiredStateSetupReport report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_reportRoot);
        var path = Path.Combine(_reportRoot, "setup-report.md");
        var blocks = SetupExecutionSummary.FromDesiredState(report);

        var builder = new StringBuilder();
        builder.AppendLine("# Estudio.Setup Report");
        builder.AppendLine();
        builder.AppendLine($"- Modo: `{options.Mode}`");
        builder.AppendLine("- Engine: `desired-state`");
        builder.AppendLine($"- Alias: `{SensitiveDataRedactor.Redact(studentAlias)}`");
        builder.AppendLine($"- Resultado: `{(report.Success ? "OK" : "ERROR")}`");
        builder.AppendLine($"- Ultimo bloque listo: `{SensitiveDataRedactor.Redact(SetupExecutionSummary.LastSuccessfulDesiredStateBlockId(report))}`");
        builder.AppendLine();
        builder.AppendLine("## Resumen");
        builder.AppendLine();
        builder.AppendLine($"- Listos: `{Count(blocks, SetupExecutionBlockStatus.Ready)}`");
        builder.AppendLine($"- Cambios aplicados: `{Count(blocks, SetupExecutionBlockStatus.Applied)}`");
        builder.AppendLine($"- Reparados: `{Count(blocks, SetupExecutionBlockStatus.Repaired)}`");
        builder.AppendLine($"- Pendientes: `{Count(blocks, SetupExecutionBlockStatus.Pending)}`");
        builder.AppendLine($"- Fallidos: `{Count(blocks, SetupExecutionBlockStatus.Failed)}`");
        builder.AppendLine();

        WriteSection(builder, "Bloques Listos", blocks, SetupExecutionBlockStatus.Ready);
        WriteSection(builder, "Cambios Aplicados", blocks, SetupExecutionBlockStatus.Applied);
        WriteSection(builder, "Bloques Reparados", blocks, SetupExecutionBlockStatus.Repaired);
        WriteSection(builder, "Bloques Pendientes", blocks, SetupExecutionBlockStatus.Pending);
        WriteSection(builder, "Bloques Fallidos", blocks, SetupExecutionBlockStatus.Failed);

        builder.AppendLine("## Detalle Tecnico");
        builder.AppendLine();
        builder.AppendLine("<details>");
        builder.AppendLine("<summary>Ver detalle tecnico</summary>");
        builder.AppendLine();
        foreach (var block in blocks)
        {
            builder.AppendLine($"- `{Escape(SensitiveDataRedactor.Redact(block.Id))}`: {Escape(SensitiveDataRedactor.Redact(block.TechnicalMessage))}");
        }

        builder.AppendLine();
        builder.AppendLine("</details>");

        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken);
        return path;
    }

    private static void WriteSection(
        StringBuilder builder,
        string title,
        IReadOnlyList<SetupExecutionBlock> blocks,
        SetupExecutionBlockStatus status)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        var matching = blocks.Where(block => block.Status == status).ToArray();
        if (matching.Length == 0)
        {
            builder.AppendLine("- Ninguno.");
            builder.AppendLine();
            return;
        }

        foreach (var block in matching)
        {
            builder.AppendLine($"- {Escape(SensitiveDataRedactor.Redact(block.Name))}: {Escape(SensitiveDataRedactor.Redact(block.HumanMessage))}");
        }

        builder.AppendLine();
    }

    private static int Count(IEnumerable<SetupExecutionBlock> blocks, SetupExecutionBlockStatus status)
    {
        return blocks.Count(block => block.Status == status);
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}