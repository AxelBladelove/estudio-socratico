using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public sealed class SetupApplicationHostTests
{
    [Fact]
    public void DesiredStateNeedsVisualHost_is_true_for_desired_state_tui_runs()
    {
        var host = new SetupApplicationHost(commandRunnerFactory: () => new NoopCommandRunner());
        var options = new SetupOptions(SetupMode.Install, TuiRequested: true, Engine: SetupExecutionEngine.DesiredState);

        Assert.True(host.DesiredStateNeedsVisualHost(options));
        Assert.False(host.ShouldRunTerminalGui(options));
    }

    [Fact]
    public void ShouldRunTerminalGui_stays_legacy_by_default()
    {
        var host = new SetupApplicationHost(commandRunnerFactory: () => new NoopCommandRunner());
        var options = new SetupOptions(SetupMode.Verify, TuiRequested: true);

        Assert.True(host.ShouldRunTerminalGui(options));
        Assert.False(host.DesiredStateNeedsVisualHost(options));
    }

    private sealed class NoopCommandRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandResult.Success(string.Empty));
        }
    }
}