using Estudio.Setup.Core;

namespace Estudio.Setup.Tests;

public class NonBlockingSetupStepTests
{
    [Fact]
    public async Task Verify_mode_converts_non_blocking_missing_step_to_warning()
    {
        var step = new OptionalStep(
            StepResult.Missing("Gemini missing"),
            verify: StepResult.Missing("Gemini still missing"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Verify), CancellationToken.None);

        Assert.True(report.Success);
        Assert.All(report.Steps, execution => Assert.True(execution.Result.IsWarning));
        Assert.Contains(report.Steps, execution => execution.Result.Message.Contains("Gemini missing"));
    }

    [Fact]
    public async Task Install_mode_continues_when_non_blocking_action_fails()
    {
        var step = new OptionalStep(
            detect: StepResult.Missing("Gemini missing"),
            install: StepResult.Fail("Gemini unavailable"),
            verify: StepResult.Missing("Gemini still missing"));
        var engine = new SetupEngine(new[] { step });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.True(report.Success);
        Assert.All(report.Steps, execution => Assert.True(execution.Result.IsWarning));
    }

    private sealed class OptionalStep : ISetupStep, INonBlockingSetupStep
    {
        private readonly StepResult _detect;
        private readonly StepResult _install;
        private readonly StepResult _verify;

        public OptionalStep(
            StepResult detect,
            StepResult? install = null,
            StepResult? verify = null)
        {
            _detect = detect;
            _install = install ?? StepResult.Ok("installed");
            _verify = verify ?? StepResult.Ok("verified");
        }

        public string Id => "gemini";
        public string Name => "Gemini";

        public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_detect);
        }

        public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_install);
        }

        public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_install);
        }

        public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_install);
        }

        public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_verify);
        }
    }
}
