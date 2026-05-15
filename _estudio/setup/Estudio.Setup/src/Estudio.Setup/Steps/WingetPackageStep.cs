using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class WingetPackageStep : ISetupStep
{
    private readonly string _packageId;
    private readonly ICommandRunner _commandRunner;
    private readonly ToolCheckStep _toolCheck;

    public WingetPackageStep(
        string id,
        string name,
        string packageId,
        string fileName,
        string versionArguments,
        ICommandRunner commandRunner)
    {
        Id = id;
        Name = name;
        _packageId = packageId;
        _commandRunner = commandRunner;
        _toolCheck = new ToolCheckStep(id, name, fileName, versionArguments, commandRunner);
    }

    public string Id { get; }
    public string Name { get; }

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return _toolCheck.DetectAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return RunWingetAsync("install", cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return RunWingetAsync("upgrade", cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return RunWingetAsync("install", cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return _toolCheck.VerifyAsync(context, cancellationToken);
    }

    private async Task<StepResult> RunWingetAsync(string verb, CancellationToken cancellationToken)
    {
        var arguments = $"{verb} --exact --id {_packageId} --silent --accept-package-agreements --accept-source-agreements";
        var result = await _commandRunner.RunAsync("winget", arguments, cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing($"winget no esta disponible para instalar {Name}.");
        }

        if (result.IsSuccess)
        {
            return StepResult.Ok($"{Name}: winget {verb} completado.");
        }

        var details = FirstLine(result.StandardError);
        if (string.IsNullOrWhiteSpace(details))
        {
            details = FirstLine(result.StandardOutput);
        }

        return StepResult.Fail($"{Name}: winget {verb} termino con codigo {result.ExitCode}. {details}");
    }

    private static string FirstLine(string text)
    {
        using var reader = new StringReader(text);
        return reader.ReadLine() ?? string.Empty;
    }
}
