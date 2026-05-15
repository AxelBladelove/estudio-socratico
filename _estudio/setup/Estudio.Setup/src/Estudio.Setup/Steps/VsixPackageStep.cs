using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class VsixPackageStep : ISetupStep
{
    private readonly string _workspaceRoot;
    private readonly ICommandRunner _commandRunner;

    public VsixPackageStep(string workspaceRoot, ICommandRunner commandRunner)
    {
        _workspaceRoot = workspaceRoot;
        _commandRunner = commandRunner;
    }

    public string Id => "vscode-extension-package";
    public string Name => "VS Code extension package";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var targetPath = VsixExtensionPaths.ResolveVsixPath(_workspaceRoot);
        if (File.Exists(targetPath))
        {
            return Task.FromResult(StepResult.Ok($"VSIX: paquete runtime listo en {targetPath}."));
        }

        return Task.FromResult(StepResult.Missing($"VSIX: falta paquete runtime en {targetPath}."));
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return PackageAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return PackageAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return PackageAsync(cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return DetectAsync(context, cancellationToken);
    }

    private async Task<StepResult> PackageAsync(CancellationToken cancellationToken)
    {
        var sourceDirectory = VsixExtensionPaths.ResolveExtensionSourceDirectory(_workspaceRoot);
        if (!Directory.Exists(sourceDirectory))
        {
            return StepResult.Missing($"VSIX: no existe el proyecto de extension en {sourceDirectory}.");
        }

        var ci = await _commandRunner.RunAsync("npm", $"ci --prefix {Quote(sourceDirectory)}", cancellationToken);
        if (!ci.WasStarted)
        {
            return StepResult.Missing("Node.js: npm no esta disponible para preparar la extension.");
        }

        if (!ci.IsSuccess)
        {
            return StepResult.Fail($"VSIX: npm ci fallo. {FirstNonEmptyLine(ci.StandardError)}");
        }

        var package = await _commandRunner.RunAsync("npm", $"run package --prefix {Quote(sourceDirectory)}", cancellationToken);
        if (!package.WasStarted)
        {
            return StepResult.Missing("Node.js: npm no esta disponible para empaquetar la extension.");
        }

        if (!package.IsSuccess)
        {
            return StepResult.Fail($"VSIX: empaquetado fallo. {FirstNonEmptyLine(package.StandardError)}");
        }

        var packagedPath = VsixExtensionPaths.ResolvePackagedVsixPath(_workspaceRoot);
        if (!File.Exists(packagedPath))
        {
            return StepResult.Fail($"VSIX: npm termino pero no genero {packagedPath}.");
        }

        var targetPath = VsixExtensionPaths.ResolveVsixPath(_workspaceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(packagedPath, targetPath, overwrite: true);

        return StepResult.Ok($"VSIX: paquete copiado a {targetPath}.");
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
