using Estudio.Setup.Core;
using Estudio.Setup.Tui;

namespace Estudio.Setup.Tests;

public sealed class SetupTuiRunPlannerTests
{
    [Fact]
    public void ForMode_preserves_paths_alias_and_tui_flag()
    {
        var baseline = new SetupOptions(
            SetupMode.Install,
            StateRoot: "state",
            AliasOverride: "axel",
            TuiRequested: true);

        var planned = SetupTuiRunPlanner.ForMode(baseline, SetupMode.Verify);

        Assert.Equal(SetupMode.Verify, planned.Mode);
        Assert.Equal("state", planned.StateRoot);
        Assert.Equal("axel", planned.AliasOverride);
        Assert.True(planned.TuiRequested);
        Assert.Null(planned.OnlyStepIds);
    }

    [Fact]
    public void RetryFailed_uses_repair_mode_and_only_failed_step_ids()
    {
        var baseline = new SetupOptions(
            SetupMode.Install,
            StateRoot: "state",
            AliasOverride: "axel",
            TuiRequested: true);

        var planned = SetupTuiRunPlanner.RetryFailed(baseline, new[] { "node", "msys2-toolchain" });

        Assert.NotNull(planned);
        Assert.Equal(SetupMode.Repair, planned.Mode);
        Assert.Equal(new[] { "node", "msys2-toolchain" }, planned.OnlyStepIds);
        Assert.Equal("state", planned.StateRoot);
        Assert.Equal("axel", planned.AliasOverride);
        Assert.True(planned.TuiRequested);
    }

    [Fact]
    public void RetryFailed_returns_null_when_there_are_no_failed_steps()
    {
        var planned = SetupTuiRunPlanner.RetryFailed(new SetupOptions(SetupMode.Install), Array.Empty<string>());

        Assert.Null(planned);
    }

    [Fact]
    public void ChangeGitHub_uses_update_mode_and_forces_relogin()
    {
        var baseline = new SetupOptions(
            SetupMode.Install,
            StateRoot: "state",
            AliasOverride: "axel",
            OnlyStepIds: new[] { "node" },
            TuiRequested: true);

        var planned = SetupTuiRunPlanner.ChangeGitHub(baseline);

        Assert.Equal(SetupMode.Update, planned.Mode);
        Assert.Equal("state", planned.StateRoot);
        Assert.Equal("axel", planned.AliasOverride);
        Assert.True(planned.TuiRequested);
        Assert.True(planned.ForceGitHubRelogin);
        Assert.Null(planned.OnlyStepIds);
    }

    [Fact]
    public void ChangeAlias_uses_update_mode_with_new_alias()
    {
        var baseline = new SetupOptions(
            SetupMode.Verify,
            StateRoot: "state",
            AliasOverride: "old",
            ForceGitHubRelogin: true);

        var planned = SetupTuiRunPlanner.ChangeAlias(baseline, "new_alias");

        Assert.Equal(SetupMode.Update, planned.Mode);
        Assert.Equal("state", planned.StateRoot);
        Assert.Equal("new_alias", planned.AliasOverride);
        Assert.True(planned.TuiRequested);
        Assert.False(planned.ForceGitHubRelogin);
        Assert.Null(planned.OnlyStepIds);
    }
}
