using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class CCompilerStepTests
{
    [Fact]
    public async Task VerifyAsync_compiles_and_runs_minimal_c_program()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success(string.Empty),
            CommandResult.Success("Estudio Socratico GCC OK"));
        var step = new CCompilerStep(runner, MakeTempRoot());

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("GCC OK", result.Message);
        Assert.Equal("gcc", runner.Calls[0].FileName);
        Assert.Contains("hello_world.c", runner.Calls[0].Arguments);
        Assert.EndsWith("hello_world.exe", runner.Calls[1].FileName);
    }

    [Fact]
    public async Task VerifyAsync_fails_when_compilation_fails()
    {
        var runner = new QueueCommandRunner(CommandResult.Failure(1, string.Empty, "syntax error"));
        var step = new CCompilerStep(runner, MakeTempRoot());

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.IsMissing);
        Assert.Contains("syntax error", result.Message);
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }

    private sealed class QueueCommandRunner : ICommandRunner
    {
        private readonly Queue<CommandResult> _results;
        private readonly List<(string FileName, string Arguments)> _calls = new();

        public QueueCommandRunner(params CommandResult[] results)
        {
            _results = new Queue<CommandResult>(results);
        }

        public IReadOnlyList<(string FileName, string Arguments)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(_results.Dequeue());
        }
    }
}
