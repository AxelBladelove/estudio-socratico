using System.Text;
using Estudio.Setup.Core;
using Estudio.Setup.Security;

namespace Estudio.Setup.State;

public sealed class FileSetupMarkdownReportWriter
{
    private readonly string _reportRoot;

    public FileSetupMarkdownReportWriter(string reportRoot)
    {
        _reportRoot = reportRoot;
    }

    public async Task<string> SaveAsync(
        SetupOptions options,
        string studentAlias,
        SetupReport report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_reportRoot);
        var path = Path.Combine(_reportRoot, "setup-report.md");

        var builder = new StringBuilder();
        builder.AppendLine("# Estudio.Setup Report");
        builder.AppendLine();
        builder.AppendLine($"- Modo: `{options.Mode}`");
        builder.AppendLine($"- Alias: `{SensitiveDataRedactor.Redact(studentAlias)}`");
        builder.AppendLine($"- Resultado: `{(report.Success ? "OK" : "ERROR")}`");
        builder.AppendLine($"- Ultimo paso exitoso: `{SensitiveDataRedactor.Redact(report.LastSuccessfulStep)}`");
        builder.AppendLine();
        builder.AppendLine("## Resumen");
        builder.AppendLine();
        builder.AppendLine($"- OK: `{report.Steps.Count(step => step.Result.Success && !step.Result.IsWarning)}`");
        builder.AppendLine($"- Faltan: `{report.Steps.Count(step => step.Result.IsMissing)}`");
        builder.AppendLine($"- Errores: `{report.Steps.Count(step => !step.Result.Success && !step.Result.IsMissing)}`");
        builder.AppendLine($"- Advertencias: `{report.Steps.Count(step => step.Result.IsWarning)}`");
        builder.AppendLine();
        builder.AppendLine("## Pendientes");
        builder.AppendLine();
        var pending = report.Steps
            .Where(step => !step.Result.Success || step.Result.IsWarning)
            .ToArray();
        if (pending.Length == 0)
        {
            builder.AppendLine("- Ninguno.");
        }
        else
        {
            foreach (var step in pending)
            {
                builder.AppendLine($"- `{Escape(SensitiveDataRedactor.Redact(step.StepId))}.{Escape(SensitiveDataRedactor.Redact(step.Phase))}`: {Escape(SensitiveDataRedactor.Redact(step.Result.Message))}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Acciones Sugeridas");
        builder.AppendLine();
        var suggestions = pending
            .Select(step => SuggestionFor(step.StepId))
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (suggestions.Length == 0)
        {
            builder.AppendLine("- Revisa el detalle y el log generado.");
        }
        else
        {
            foreach (var suggestion in suggestions)
            {
                builder.AppendLine($"- {suggestion}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Detalle");
        builder.AppendLine();
        builder.AppendLine("| Estado | Componente | Fase | Mensaje |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (var step in report.Steps)
        {
            builder.AppendLine($"| {MarkerFor(step.Result)} | {Escape(SensitiveDataRedactor.Redact(step.StepId))} | {Escape(SensitiveDataRedactor.Redact(step.Phase))} | {Escape(SensitiveDataRedactor.Redact(step.Result.Message))} |");
        }

        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken);
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

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string? SuggestionFor(string stepId)
    {
        return stepId switch
        {
            "github-auth" => "`github-auth`: ejecuta `gh auth login` y vuelve a verificar.",
            "github-fork" => "`github-fork`: crear o verificar el fork esperado en GitHub.",
            "git-remotes" => "`git-remotes`: reparar origin/upstream con el modo `repair` cuando estes listo.",
            "git-project-update" => "`git-project-update`: confirma que `upstream` existe y apunta al repo principal.",
            "vscode-settings" => "`vscode-settings`: reparar settings de VS Code con backup automatico.",
            "exercism-cli" => "`exercism-cli`: instalar Exercism CLI con winget o reintentar desde la TUI.",
            "exercism-c-track" => "`exercism-c-track`: pegar el token desde https://exercism.org/settings/api_cli; si falta unir el track, abrir https://exercism.org/tracks/c y reintentar fallidos.",
            "msys2-toolchain" => "`msys2-toolchain`: instalar o reparar MSYS2 UCRT64/GCC.",
            "user-path" => "`user-path`: agregar `C:\\msys64\\ucrt64\\bin` al PATH de usuario.",
            "gemini-runtime-config" => "`gemini-runtime-config`: proveer runtime-config.private.json o runtime-config.bootstrap.json y ejecutar instalacion cuando este disponible.",
            "exercise-catalog" => "`exercise-catalog`: regenerar o restaurar el catalogo Alejandro/Gists.",
            "local-alias" => "`local-alias`: corregir .estudio_usuario o usar `--alias <valor>`.",
            _ => null,
        };
    }
}
