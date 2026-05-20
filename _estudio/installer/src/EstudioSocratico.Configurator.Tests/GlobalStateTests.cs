using EstudioSocratico.Configurator.Core;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public class GlobalStateTests
{
    private static List<DependencyState> AllReady() =>
    [
        new() { Id = DependencyId.Git, DisplayName = "Git", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.GitHubCli, DisplayName = "GitHub CLI", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.ExercismCli, DisplayName = "Exercism CLI", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.VSCode, DisplayName = "VS Code", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.Msys2, DisplayName = "MSYS2", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.Gcc, DisplayName = "GCC", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.Make, DisplayName = "Make", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.Winget, DisplayName = "WinGet", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.NodeJs, DisplayName = "Node.js", Status = DependencyStatus.Ready },
        new() { Id = DependencyId.Python, DisplayName = "Python", Status = DependencyStatus.Ready },
    ];

    private static AccountState AuthOk() => new() { Configured = true, UserName = "testuser" };

    [Fact]
    public void AllCriticalReady_AuthOk_Workspace_BuildFlow_Returns_ReadyToStudy()
    {
        var result = GlobalStateCalculator.Calculate(
            AllReady(), AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.ReadyToStudy, result);
    }

    [Fact]
    public void ExercismMissing_Returns_NeedsSetup_Not_ReadyToStudy()
    {
        var deps = AllReady();
        deps[2] = deps[2] with { Status = DependencyStatus.Missing };

        var result = GlobalStateCalculator.Calculate(
            deps, AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.NeedsSetup, result);
        Assert.NotEqual(GlobalState.ReadyToStudy, result);
    }

    [Fact]
    public void VSCodeBroken_Returns_NeedsRepair()
    {
        var deps = AllReady();
        deps[3] = deps[3] with { Status = DependencyStatus.Broken };

        var result = GlobalStateCalculator.Calculate(
            deps, AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.NeedsRepair, result);
    }

    [Fact]
    public void GitHubNotAuthenticated_Returns_NeedsAuthentication()
    {
        var github = new AccountState { Configured = false };

        var result = GlobalStateCalculator.Calculate(
            AllReady(), github, AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.NeedsAuthentication, result);
    }

    [Fact]
    public void ExercismNotAuthenticated_Returns_NeedsAuthentication()
    {
        var exercism = new AccountState { Configured = false };

        var result = GlobalStateCalculator.Calculate(
            AllReady(), AuthOk(), exercism, workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.NeedsAuthentication, result);
    }

    [Fact]
    public void OnlyOptionalMissing_Returns_PartiallyReady()
    {
        var deps = AllReady();
        // NodeJs is optional
        deps[8] = deps[8] with { Status = DependencyStatus.Missing };

        var result = GlobalStateCalculator.Calculate(
            deps, AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.PartiallyReady, result);
    }

    [Fact]
    public void CriticalFailed_Returns_Failed()
    {
        var deps = AllReady();
        deps[0] = deps[0] with { Status = DependencyStatus.Failed };

        var result = GlobalStateCalculator.Calculate(
            deps, AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.Failed, result);
    }

    [Fact]
    public void WorkspaceInvalid_Returns_NeedsUserAction()
    {
        var result = GlobalStateCalculator.Calculate(
            AllReady(), AuthOk(), AuthOk(), workspaceValid: false, buildFlowValid: true);

        Assert.Equal(GlobalState.NeedsUserAction, result);
    }

    [Fact]
    public void BuildFlowInvalid_Returns_NeedsUserAction()
    {
        var result = GlobalStateCalculator.Calculate(
            AllReady(), AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: false);

        Assert.Equal(GlobalState.NeedsUserAction, result);
    }

    [Fact]
    public void GccOutdated_Returns_NeedsRepair()
    {
        var deps = AllReady();
        deps[5] = deps[5] with { Status = DependencyStatus.Outdated };

        var result = GlobalStateCalculator.Calculate(
            deps, AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.NeedsRepair, result);
    }

    [Fact]
    public void MissingPrioritizedOverBroken()
    {
        var deps = AllReady();
        deps[0] = deps[0] with { Status = DependencyStatus.Broken };
        deps[2] = deps[2] with { Status = DependencyStatus.Missing };

        var result = GlobalStateCalculator.Calculate(
            deps, AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        // Missing takes priority over Broken (NeedsSetup before NeedsRepair)
        Assert.Equal(GlobalState.NeedsSetup, result);
    }

    [Fact]
    public void FailedPrioritizedOverMissing()
    {
        var deps = AllReady();
        deps[0] = deps[0] with { Status = DependencyStatus.Failed };
        deps[2] = deps[2] with { Status = DependencyStatus.Missing };

        var result = GlobalStateCalculator.Calculate(
            deps, AuthOk(), AuthOk(), workspaceValid: true, buildFlowValid: true);

        Assert.Equal(GlobalState.Failed, result);
    }

    [Fact]
    public void HumanMessage_ReadyToStudy()
    {
        var msg = GlobalStateCalculator.GetHumanMessage(GlobalState.ReadyToStudy);
        Assert.Contains("listo", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HumanMessage_NeedsSetup_NotListo()
    {
        var msg = GlobalStateCalculator.GetHumanMessage(GlobalState.NeedsSetup);
        Assert.DoesNotContain("listo", msg, StringComparison.OrdinalIgnoreCase);
    }
}
