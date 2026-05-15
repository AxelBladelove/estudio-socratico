using Estudio.Setup.Core;
using Estudio.Setup.State;

namespace Estudio.Setup.Tests;

public class SetupMarkdownReportWriterTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "EstudioSetupTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_writes_copyable_markdown_report()
    {
        var writer = new FileSetupMarkdownReportWriter(_tempRoot);
        var report = SetupReport.Failed("git", new[]
        {
            new StepExecution("git", "verify", StepResult.Ok("git ok")),
            new StepExecution("github-fork", "detect", StepResult.Missing("fork missing")),
            new StepExecution("gemini-runtime-config", "verify", StepResult.Warning("gemini missing")),
        });

        var path = await writer.SaveAsync(new SetupOptions(SetupMode.Verify), "axel", report, CancellationToken.None);

        Assert.Equal(Path.Combine(_tempRoot, "setup-report.md"), path);
        var markdown = await File.ReadAllTextAsync(path);
        Assert.Contains("# Estudio.Setup Report", markdown);
        Assert.Contains("- Modo: `Verify`", markdown);
        Assert.Contains("- Alias: `axel`", markdown);
        Assert.Contains("- Resultado: `ERROR`", markdown);
        Assert.Contains("## Resumen", markdown);
        Assert.Contains("- OK: `1`", markdown);
        Assert.Contains("- Faltan: `1`", markdown);
        Assert.Contains("- Advertencias: `1`", markdown);
        Assert.Contains("## Pendientes", markdown);
        Assert.Contains("- `github-fork.detect`: fork missing", markdown);
        Assert.Contains("- `gemini-runtime-config.verify`: gemini missing", markdown);
        Assert.Contains("## Acciones Sugeridas", markdown);
        Assert.Contains("github-fork", markdown);
        Assert.Contains("crear o verificar el fork", markdown);
        Assert.Contains("runtime-config.bootstrap.json", markdown);
        Assert.Contains("## Detalle", markdown);
        Assert.Contains("| OK | git | verify | git ok |", markdown);
        Assert.Contains("| FALTA | github-fork | detect | fork missing |", markdown);
        Assert.Contains("| ADVERTENCIA | gemini-runtime-config | verify | gemini missing |", markdown);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
