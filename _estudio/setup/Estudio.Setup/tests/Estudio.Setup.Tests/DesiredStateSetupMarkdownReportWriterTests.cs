using Estudio.Setup.Core;
using Estudio.Setup.State;

namespace Estudio.Setup.Tests;

public sealed class DesiredStateSetupMarkdownReportWriterTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"estudio-setup-report-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_keeps_internal_step_names_only_inside_technical_details()
    {
        var writer = new DesiredStateSetupMarkdownReportWriter(_tempRoot);
        var report = new DesiredStateSetupReport(
            Success: false,
            new[]
            {
                new DesiredStateNodeReport(
                    "workspace-ready",
                    "tu carpeta de estudio",
                    new SetupNodeResult(
                        "workspace-ready",
                        "tu carpeta de estudio",
                        SetupNodeStatus.ActionRequired,
                        "Voy a preparar tu carpeta de estudio.",
                        "git-remote-step failed",
                        Array.Empty<StepExecution>()),
                    new SetupNodePlan(
                        "workspace-ready",
                        "tu carpeta de estudio",
                        SetupNodeStatus.ActionRequired,
                        "Voy a preparar tu carpeta de estudio.",
                        "git-remote-step failed",
                        RequiresChanges: true,
                        ApplyActions: new[] { new SetupPlannedAction("git-remote-step", "install") },
                        RepairActions: Array.Empty<SetupRepairAction>()),
                    null,
                    null,
                    new SetupNodeResult(
                        "workspace-ready",
                        "tu carpeta de estudio",
                        SetupNodeStatus.Failed,
                        "No pude completar tu carpeta de estudio.",
                        "git-remote-step failed",
                        Array.Empty<StepExecution>())),
            });

        var path = await writer.SaveAsync(new SetupOptions(SetupMode.Install, Engine: SetupExecutionEngine.DesiredState), "axel", report, CancellationToken.None);
        var text = await File.ReadAllTextAsync(path);

        Assert.Contains("## Bloques Fallidos", text);
        Assert.Contains("tu carpeta de estudio: No pude completar tu carpeta de estudio.", text);
        Assert.Contains("<details>", text);

        var detailsIndex = text.IndexOf("<details>", StringComparison.Ordinal);
        var internalNameIndex = text.IndexOf("git-remote-step failed", StringComparison.Ordinal);
        Assert.True(detailsIndex >= 0);
        Assert.True(internalNameIndex > detailsIndex);
    }

    [Fact]
    public async Task SaveAsync_redacts_secret_like_values_from_technical_details()
    {
        var writer = new DesiredStateSetupMarkdownReportWriter(_tempRoot);
        var report = new DesiredStateSetupReport(
            Success: false,
            new[]
            {
                new DesiredStateNodeReport(
                    "exercises-ready",
                    "tus ejercicios",
                    new SetupNodeResult(
                        "exercises-ready",
                        "tus ejercicios",
                        SetupNodeStatus.Failed,
                        "No pude dejar Exercism listo.",
                        "exercism configure --token token-1234567890abcdefghijklmnop",
                        Array.Empty<StepExecution>()),
                    new SetupNodePlan(
                        "exercises-ready",
                        "tus ejercicios",
                        SetupNodeStatus.Failed,
                        "No pude dejar Exercism listo.",
                        "exercism configure --token token-1234567890abcdefghijklmnop",
                        RequiresChanges: true,
                        ApplyActions: Array.Empty<SetupPlannedAction>(),
                        RepairActions: Array.Empty<SetupRepairAction>()),
                    null,
                    null,
                    new SetupNodeResult(
                        "exercises-ready",
                        "tus ejercicios",
                        SetupNodeStatus.Failed,
                        "No pude dejar Exercism listo.",
                        "exercism configure --token token-1234567890abcdefghijklmnop",
                        Array.Empty<StepExecution>())),
            });

        var path = await writer.SaveAsync(new SetupOptions(SetupMode.Install, Engine: SetupExecutionEngine.DesiredState), "axel", report, CancellationToken.None);
        var text = await File.ReadAllTextAsync(path);

        Assert.DoesNotContain("token-1234567890abcdefghijklmnop", text, StringComparison.Ordinal);
        Assert.Contains("EXERCISM_TOKEN_REDACTED", text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}