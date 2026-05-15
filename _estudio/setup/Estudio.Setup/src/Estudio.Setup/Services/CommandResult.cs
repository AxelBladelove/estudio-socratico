namespace Estudio.Setup.Services;

public sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool WasStarted)
{
    public bool IsSuccess => WasStarted && ExitCode == 0;

    public static CommandResult Success(string standardOutput) => new(0, standardOutput, string.Empty, true);

    public static CommandResult Failure(int exitCode, string standardOutput, string standardError)
    {
        return new(exitCode, standardOutput, standardError, true);
    }

    public static CommandResult NotFound(string fileName)
    {
        var message = Path.IsPathFullyQualified(fileName)
            ? $"{fileName} no existe o no se puede ejecutar."
            : $"{fileName} no esta disponible en PATH.";

        return new(-1, string.Empty, message, false);
    }
}
