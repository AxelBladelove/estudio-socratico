using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class ToolCheckStep : ISetupStep
{
    private readonly string _fileName;
    private readonly string _versionArguments;
    private readonly ICommandRunner _commandRunner;

    public ToolCheckStep(
        string id,
        string name,
        string fileName,
        string versionArguments,
        ICommandRunner commandRunner)
    {
        Id = id;
        Name = name;
        _fileName = fileName;
        _versionArguments = versionArguments;
        _commandRunner = commandRunner;
    }

    public string Id { get; }
    public string Name { get; }

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckToolAsync(cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepResult.Fail($"{Name}: instalacion automatica pendiente de implementar."));
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepResult.Fail($"{Name}: actualizacion automatica pendiente de implementar."));
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepResult.Fail($"{Name}: reparacion automatica pendiente de implementar."));
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckToolAsync(cancellationToken);
    }

    private async Task<StepResult> CheckToolAsync(CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(_fileName, _versionArguments, cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing($"{Name}: {FirstNonEmptyLine(result.StandardError)}");
        }

        var message = FirstNonEmptyLine(result.StandardOutput);
        if (result.IsSuccess)
        {
            return StepResult.Ok($"{Name}: {message}");
        }

        var details = FirstNonEmptyLine(result.StandardError);
        if (string.IsNullOrWhiteSpace(details))
        {
            details = message;
        }

        return StepResult.Fail($"{Name}: comando termino con codigo {result.ExitCode}. {details}");
    }

    private static string FirstNonEmptyLine(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim();
            }
        }

        return string.Empty;
    }
}
