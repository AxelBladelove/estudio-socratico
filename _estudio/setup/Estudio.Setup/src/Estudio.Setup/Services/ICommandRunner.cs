namespace Estudio.Setup.Services;

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken);
}
