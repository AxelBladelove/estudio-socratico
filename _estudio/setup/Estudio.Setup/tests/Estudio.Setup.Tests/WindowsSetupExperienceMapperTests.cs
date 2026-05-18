using Estudio.Setup.Core;
using Estudio.Setup.Windows;

namespace Estudio.Setup.Tests;

public sealed class WindowsSetupExperienceMapperTests
{
    [Fact]
    public async Task ApplyProgress_maps_node_events_to_human_blocks_without_exposing_internal_ids()
    {
        var blocks = WindowsSetupExperienceMapper.CreateBlocks();
        var result = new SetupNodeResult(
            "compiler-ready",
            "el compilador de C",
            SetupNodeStatus.Failed,
            "No pude instalar MSYS2 automaticamente.",
            "msys2-toolchain failed",
            Array.Empty<StepExecution>());

        WindowsSetupExperienceMapper.ApplyProgress(
            blocks,
            new DesiredStateNodePhaseFinished("compiler-ready", "el compilador de C", "verify", result));

        var block = blocks.First(entry => entry.Id == "compiler-ready");
        Assert.Equal("No pude instalar MSYS2 automaticamente.", WindowsSetupExperienceMapper.HumanFailureMessage(block));
        Assert.DoesNotContain("msys2-toolchain", block.HumanMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("msys2-toolchain", block.TechnicalMessage, StringComparison.OrdinalIgnoreCase);
        await Task.CompletedTask;
    }

    [Fact]
    public void GuidedHelpUrl_returns_exercism_pages_for_exercises_block()
    {
        var block = new InstallerProgressBlock("exercises-ready", "Preparando ejercicios", "Preparando ejercicios...")
        {
            TechnicalMessage = Steps.ExercismCTrackStep.CTrackUrl,
            Status = SetupExecutionBlockStatus.Failed,
        };

        var url = WindowsSetupExperienceMapper.GuidedHelpUrl(block);

        Assert.Equal(Steps.ExercismCTrackStep.CTrackUrl, url);
    }

    [Fact]
    public void ApplyProgress_redacts_secret_like_technical_details()
    {
        var blocks = WindowsSetupExperienceMapper.CreateBlocks();
        var result = new SetupNodeResult(
            "exercises-ready",
            "tus ejercicios",
            SetupNodeStatus.Failed,
            "No pude dejar Exercism listo.",
            "exercism configure --token token-1234567890abcdefghijklmnop",
            Array.Empty<StepExecution>());

        WindowsSetupExperienceMapper.ApplyProgress(
            blocks,
            new DesiredStateNodePhaseFinished("exercises-ready", "tus ejercicios", "apply", result));

        var block = blocks.First(entry => entry.Id == "exercises-ready");
        Assert.DoesNotContain("token-1234567890abcdefghijklmnop", block.TechnicalMessage, StringComparison.Ordinal);
        Assert.Contains("EXERCISM_TOKEN_REDACTED", block.TechnicalMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("github-ready")]
    [InlineData("workspace-ready")]
    [InlineData("vscode-ready")]
    [InlineData("extension-ready")]
    [InlineData("compiler-ready")]
    [InlineData("exercises-ready")]
    [InlineData("gemini-ready")]
    [InlineData("f9-ready")]
    public void GuidedSolutionCatalog_contains_human_solution_for_each_supported_block(string blockId)
    {
        var solution = GuidedSolutionCatalog.ForBlock(blockId);

        Assert.NotNull(solution);
        Assert.NotEmpty(solution!.Steps);
    }
}