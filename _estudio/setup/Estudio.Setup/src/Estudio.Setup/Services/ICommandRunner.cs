namespace Estudio.Setup.Services;

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        CommandExecutionOptions executionOptions,
        CancellationToken cancellationToken);

    Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        return RunAsync(fileName, arguments, CommandExecutionOptions.Default, cancellationToken);
    }
}
