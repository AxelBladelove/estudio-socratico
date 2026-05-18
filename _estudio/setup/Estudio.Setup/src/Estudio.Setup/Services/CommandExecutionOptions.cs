namespace Estudio.Setup.Services;

public sealed record CommandExecutionOptions(
    string? WorkingDirectory = null)
{
    public static CommandExecutionOptions Default { get; } = new();
}