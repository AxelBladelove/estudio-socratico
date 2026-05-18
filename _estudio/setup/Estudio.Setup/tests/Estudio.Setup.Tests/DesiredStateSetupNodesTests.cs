using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public sealed class DesiredStateSetupNodesTests
{
    [Fact]
    public async Task WorkspaceReadyNode_plans_actions_for_incomplete_steps()
    {
        var node = new WorkspaceReadyNode(new ISetupStep[]
        {
            new FakeStep("git-workspace", detect: StepResult.Missing("git-workspace missing")),
            new FakeStep("git-identity", detect: StepResult.Ok("identity ok")),
            new FakeStep("git-remotes", detect: StepResult.Warning("remotes drifted")),
        });

        var detected = await node.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);
        var plan = await node.PlanAsync(new SetupContext(new SetupOptions(SetupMode.Install)), detected, CancellationToken.None);

        Assert.Equal(SetupNodeStatus.ActionRequired, detected.Status);
        Assert.True(plan.RequiresChanges);
        Assert.Equal(new[] { "git-workspace", "git-remotes" }, plan.ApplyActions.Select(action => action.StepId));
    }

    [Fact]
    public async Task ExtensionReadyNode_human_message_hides_internal_step_names()
    {
        var node = new ExtensionReadyNode(new ISetupStep[]
        {
            new FakeStep("git-remote-step", detect: StepResult.Missing("git-remote-step failed badly")),
        });

        var detected = await node.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.DoesNotContain("git-remote-step", detected.HumanMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("git-remote-step", detected.TechnicalMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GitHubReadyNode_repair_retries_non_ready_steps()
    {
        var broken = new FakeStep(
            "github-auth",
            detect: StepResult.Fail("gh auth broken"),
            repair: StepResult.Ok("gh auth repaired"));
        var node = new GitHubReadyNode(new ISetupStep[]
        {
            new FakeStep("github-cli", detect: StepResult.Ok("gh ok")),
            broken,
        });

        var detected = await node.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Repair)), CancellationToken.None);
        var plan = await node.PlanAsync(new SetupContext(new SetupOptions(SetupMode.Repair)), detected, CancellationToken.None);
        var result = await node.RepairAsync(new SetupContext(new SetupOptions(SetupMode.Repair)), Assert.Single(plan.RepairActions), CancellationToken.None);

        Assert.Equal(SetupNodeStatus.RepairRequired, detected.Status);
        Assert.True(result.IsReady);
        Assert.Equal(1, broken.RepairCalls);
    }

    private sealed class FakeStep : ISetupStep
    {
        private readonly StepResult _detect;
        private readonly StepResult _install;
        private readonly StepResult _repair;
        private readonly StepResult _verify;

        public FakeStep(
            string id,
            StepResult? detect = null,
            StepResult? install = null,
            StepResult? repair = null,
            StepResult? verify = null)
        {
            Id = id;
            Name = id;
            _detect = detect ?? StepResult.Ok("detected");
            _install = install ?? StepResult.Ok("installed");
            _repair = repair ?? StepResult.Ok("repaired");
            _verify = verify ?? StepResult.Ok("verified");
        }

        public string Id { get; }
        public string Name { get; }
        public int RepairCalls { get; private set; }

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
            RepairCalls++;
            return Task.FromResult(_repair);
        }

        public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_verify);
        }
    }
}