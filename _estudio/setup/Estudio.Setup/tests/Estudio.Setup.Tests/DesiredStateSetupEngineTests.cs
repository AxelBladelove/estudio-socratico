using Estudio.Setup.Core;

namespace Estudio.Setup.Tests;

public sealed class DesiredStateSetupEngineTests
{
    [Fact]
    public async Task RunAsync_skips_apply_when_node_is_already_ready()
    {
        var node = new ScriptedNode(
            id: "workspace-ready",
            name: "workspace",
            detect: Ready("workspace-ready", "workspace", "Tu carpeta de estudio ya esta lista."),
            plan: new SetupNodePlan(
                "workspace-ready",
                "workspace",
                SetupNodeStatus.Ready,
                "Tu carpeta de estudio ya esta lista.",
                "workspace-ready.detect: ok",
                RequiresChanges: false,
                ApplyActions: Array.Empty<SetupPlannedAction>(),
                RepairActions: Array.Empty<SetupRepairAction>()),
            verify: Ready("workspace-ready", "workspace", "Tu carpeta de estudio ya esta lista."));
        var engine = new DesiredStateSetupEngine(new[] { node });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(0, node.ApplyCalls);
        Assert.Equal(1, node.VerifyCalls);
    }

    [Fact]
    public async Task RunAsync_applies_changes_for_incomplete_node()
    {
        var node = new ScriptedNode(
            id: "extension-ready",
            name: "extension",
            detect: Missing("extension-ready", "extension", "Voy a instalar la extension de VS Code."),
            plan: new SetupNodePlan(
                "extension-ready",
                "extension",
                SetupNodeStatus.ActionRequired,
                "Voy a instalar la extension de VS Code.",
                "vscode-extension-package.detect: missing",
                RequiresChanges: true,
                ApplyActions: new[] { new SetupPlannedAction("vscode-extension-package", "install") },
                RepairActions: new[] { new SetupRepairAction("extension-ready-repair", "Voy a reparar la extension de VS Code.", "repair extension") }),
            apply: Ready("extension-ready", "extension", "La extension de VS Code quedo instalada."),
            verify: Ready("extension-ready", "extension", "La extension de VS Code ya esta lista."));
        var engine = new DesiredStateSetupEngine(new[] { node });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(1, node.ApplyCalls);
        Assert.Equal("apply", Assert.Single(report.Nodes).ActionPhase);
    }

    [Fact]
    public async Task RunAsync_continues_after_failed_node_and_preserves_other_results()
    {
        var first = new ScriptedNode(
            id: "github-ready",
            name: "github",
            detect: Ready("github-ready", "github", "Tu copia en GitHub ya esta lista."),
            plan: ReadyPlan("github-ready", "github", "Tu copia en GitHub ya esta lista."),
            verify: Ready("github-ready", "github", "Tu copia en GitHub ya esta lista."));
        var failed = new ScriptedNode(
            id: "workspace-ready",
            name: "workspace",
            detect: Missing("workspace-ready", "workspace", "Voy a preparar tu carpeta de estudio."),
            plan: new SetupNodePlan(
                "workspace-ready",
                "workspace",
                SetupNodeStatus.ActionRequired,
                "Voy a preparar tu carpeta de estudio.",
                "git-workspace.detect: missing",
                RequiresChanges: true,
                ApplyActions: new[] { new SetupPlannedAction("git-workspace", "install") },
                RepairActions: new[] { new SetupRepairAction("workspace-ready-repair", "Voy a reparar tu carpeta de estudio.", "repair workspace") }),
            apply: Failed("workspace-ready", "workspace", "No pude completar tu carpeta de estudio."),
            verify: Missing("workspace-ready", "workspace", "Voy a preparar tu carpeta de estudio."));
        var last = new ScriptedNode(
            id: "compiler-ready",
            name: "compiler",
            detect: Ready("compiler-ready", "compiler", "El compilador de C ya esta listo."),
            plan: ReadyPlan("compiler-ready", "compiler", "El compilador de C ya esta listo."),
            verify: Ready("compiler-ready", "compiler", "El compilador de C ya esta listo."));
        var engine = new DesiredStateSetupEngine(new[] { first, failed, last });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Install), CancellationToken.None);

        Assert.False(report.Success);
        Assert.Equal(1, last.VerifyCalls);
        Assert.Equal(new[] { "github-ready", "workspace-ready", "compiler-ready" }, report.Nodes.Select(node => node.NodeId));
    }

    [Fact]
    public async Task RunAsync_uses_repair_when_mode_is_repair()
    {
        var node = new ScriptedNode(
            id: "workspace-ready",
            name: "workspace",
            detect: RepairRequired("workspace-ready", "workspace", "Voy a reparar tu carpeta de estudio."),
            plan: new SetupNodePlan(
                "workspace-ready",
                "workspace",
                SetupNodeStatus.RepairRequired,
                "Voy a reparar tu carpeta de estudio.",
                "git-remotes.detect: failed",
                RequiresChanges: true,
                ApplyActions: Array.Empty<SetupPlannedAction>(),
                RepairActions: new[] { new SetupRepairAction("workspace-ready-repair", "Voy a reparar tu carpeta de estudio.", "repair workspace") }),
            repair: Ready("workspace-ready", "workspace", "Tu carpeta de estudio fue reparada."),
            verify: Ready("workspace-ready", "workspace", "Tu carpeta de estudio ya esta lista."));
        var engine = new DesiredStateSetupEngine(new[] { node });

        var report = await engine.RunAsync(new SetupOptions(SetupMode.Repair), CancellationToken.None);

        Assert.True(report.Success);
        Assert.Equal(1, node.RepairCalls);
        Assert.Equal("repair", Assert.Single(report.Nodes).ActionPhase);
    }

    private static SetupNodeResult Ready(string nodeId, string nodeName, string humanMessage)
    {
        return new SetupNodeResult(nodeId, nodeName, SetupNodeStatus.Ready, humanMessage, $"{nodeId}: ok", Array.Empty<StepExecution>());
    }

    private static SetupNodeResult Missing(string nodeId, string nodeName, string humanMessage)
    {
        return new SetupNodeResult(nodeId, nodeName, SetupNodeStatus.ActionRequired, humanMessage, $"{nodeId}: missing", Array.Empty<StepExecution>());
    }

    private static SetupNodeResult RepairRequired(string nodeId, string nodeName, string humanMessage)
    {
        return new SetupNodeResult(nodeId, nodeName, SetupNodeStatus.RepairRequired, humanMessage, $"{nodeId}: repair", Array.Empty<StepExecution>());
    }

    private static SetupNodeResult Failed(string nodeId, string nodeName, string humanMessage)
    {
        return new SetupNodeResult(nodeId, nodeName, SetupNodeStatus.Failed, humanMessage, $"{nodeId}: failed", Array.Empty<StepExecution>());
    }

    private static SetupNodePlan ReadyPlan(string nodeId, string nodeName, string humanMessage)
    {
        return new SetupNodePlan(
            nodeId,
            nodeName,
            SetupNodeStatus.Ready,
            humanMessage,
            $"{nodeId}: ready",
            RequiresChanges: false,
            ApplyActions: Array.Empty<SetupPlannedAction>(),
            RepairActions: Array.Empty<SetupRepairAction>());
    }

    private sealed class ScriptedNode : ISetupStateNode
    {
        private readonly SetupNodeResult _detect;
        private readonly SetupNodePlan _plan;
        private readonly SetupNodeResult _apply;
        private readonly SetupNodeResult _repair;
        private readonly SetupNodeResult _verify;

        public ScriptedNode(
            string id,
            string name,
            SetupNodeResult detect,
            SetupNodePlan plan,
            SetupNodeResult? apply = null,
            SetupNodeResult? repair = null,
            SetupNodeResult? verify = null)
        {
            Id = id;
            Name = name;
            _detect = detect;
            _plan = plan;
            _apply = apply ?? detect;
            _repair = repair ?? detect;
            _verify = verify ?? detect;
        }

        public string Id { get; }
        public string Name { get; }
        public int ApplyCalls { get; private set; }
        public int RepairCalls { get; private set; }
        public int VerifyCalls { get; private set; }

        public Task<SetupNodeResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_detect);
        }

        public Task<SetupNodePlan> PlanAsync(SetupContext context, SetupNodeResult detectedState, CancellationToken cancellationToken)
        {
            return Task.FromResult(_plan);
        }

        public Task<SetupNodeResult> ApplyAsync(SetupContext context, SetupNodePlan plan, CancellationToken cancellationToken)
        {
            ApplyCalls++;
            return Task.FromResult(_apply);
        }

        public Task<SetupNodeResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
        {
            VerifyCalls++;
            return Task.FromResult(_verify);
        }

        public Task<SetupNodeResult> RepairAsync(SetupContext context, SetupRepairAction repairAction, CancellationToken cancellationToken)
        {
            RepairCalls++;
            return Task.FromResult(_repair);
        }
    }
}