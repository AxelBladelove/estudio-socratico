using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class VsixExtensionStep : ISetupStep, IUninstallSetupStep
{
    private readonly string _vsixPath;
    private readonly string _extensionId;
    private readonly ICommandRunner _commandRunner;
    private readonly string _codeCommand;

    public VsixExtensionStep(
        string vsixPath,
        string extensionId,
        ICommandRunner commandRunner,
        string codeCommand = "code")
    {
        _vsixPath = vsixPath;
        _extensionId = extensionId;
        _commandRunner = commandRunner;
        _codeCommand = codeCommand;
    }

    public string Id => "vscode-extension";
    public string Name => "VS Code extension";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        if (!File.Exists(_vsixPath))
        {
            return Task.FromResult(StepResult.Missing($"VSIX: no existe {_vsixPath}."));
        }

        return Task.FromResult(StepResult.Ok($"VSIX: paquete encontrado en {_vsixPath}."));
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return InstallOrRepairAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return InstallOrRepairAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return InstallOrRepairAsync(cancellationToken);
    }

    public async Task<StepResult> UninstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(
            _codeCommand,
            $"--uninstall-extension {_extensionId}",
            cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Warning("VS Code: code no esta disponible; no se pudo confirmar la extension.");
        }

        if (!result.IsSuccess)
        {
            var detail = FirstNonEmptyLine(result.StandardError);
            if (detail.Contains("not installed", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("no esta instalada", StringComparison.OrdinalIgnoreCase))
            {
                return StepResult.Warning($"VSIX: extension {_extensionId} ya no estaba instalada.");
            }

            return StepResult.Fail($"VSIX: desinstalacion fallo. {detail}");
        }

        return StepResult.Ok($"VSIX: extension {_extensionId} desinstalada.");
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(_codeCommand, "--list-extensions", cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing("VS Code: code no esta disponible para listar extensiones.");
        }

        if (!result.IsSuccess)
        {
            return StepResult.Fail($"VS Code: no se pudieron listar extensiones. {FirstNonEmptyLine(result.StandardError)}");
        }

        var installed = result.StandardOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line => string.Equals(line.Trim(), _extensionId, StringComparison.OrdinalIgnoreCase));

        return installed
            ? StepResult.Ok($"VSIX: extension {_extensionId} instalada.")
            : StepResult.Missing($"VSIX: extension {_extensionId} no esta instalada.");
    }

    private async Task<StepResult> InstallOrRepairAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_vsixPath))
        {
            return StepResult.Missing($"VSIX: no existe {_vsixPath}.");
        }

        var result = await _commandRunner.RunAsync(
            _codeCommand,
            $"--install-extension {Quote(_vsixPath)} --force",
            cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing("VS Code: code no esta disponible para instalar el VSIX.");
        }

        if (!result.IsSuccess)
        {
            return StepResult.Fail($"VSIX: instalacion fallo. {FirstNonEmptyLine(result.StandardError)}");
        }

        return StepResult.Ok($"VSIX: extension instalada desde {_vsixPath}.");
    }

    private static string Quote(string value) => $"\"{value}\"";

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
