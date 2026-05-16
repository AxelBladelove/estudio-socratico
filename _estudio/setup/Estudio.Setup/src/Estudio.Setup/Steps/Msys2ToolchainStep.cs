using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class Msys2ToolchainStep : ISetupStep
{
    public const string PacmanPath = @"C:\msys64\usr\bin\pacman.exe";
    public const string Ucrt64Bin = @"C:\msys64\ucrt64\bin";
    public const string GccPath = Ucrt64Bin + @"\gcc.exe";
    public const string MakePath = Ucrt64Bin + @"\mingw32-make.exe";
    public const string GdbPath = Ucrt64Bin + @"\gdb.exe";

    private readonly ICommandRunner _commandRunner;
    private readonly CCompilerStep _compilerStep;
    private readonly ToolCheckStep _pacmanCheck;
    private readonly ToolCheckStep _gccCheck;
    private readonly ToolCheckStep _makeCheck;
    private readonly ToolCheckStep _gdbCheck;

    public Msys2ToolchainStep(ICommandRunner commandRunner, string? compilerWorkRoot = null)
    {
        _commandRunner = commandRunner;
        _compilerStep = new CCompilerStep(commandRunner, compilerWorkRoot, GccPath);
        _pacmanCheck = new ToolCheckStep(Id, "MSYS2 Pacman", PacmanPath, "--version", commandRunner);
        _gccCheck = new ToolCheckStep(Id, "GCC UCRT64", GccPath, "--version", commandRunner);
        _makeCheck = new ToolCheckStep(Id, "Make UCRT64", MakePath, "--version", commandRunner);
        _gdbCheck = new ToolCheckStep(Id, "GDB UCRT64", GdbPath, "--version", commandRunner);
    }

    public string Id => "msys2-toolchain";
    public string Name => "MSYS2 UCRT64 toolchain";

    public async Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return await RunToolChecksAsync(
            context,
            check => check.DetectAsync(context, cancellationToken));
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureToolchainAsync(
            context,
            installMsys2IfMissing: true,
            refreshPackageDatabases: true,
            modeLabel: "instalar",
            cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureToolchainAsync(
            context,
            installMsys2IfMissing: false,
            refreshPackageDatabases: false,
            modeLabel: "actualizar",
            cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureToolchainAsync(
            context,
            installMsys2IfMissing: true,
            refreshPackageDatabases: false,
            modeLabel: "reinstalar",
            cancellationToken);
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var tools = await RunToolChecksAsync(
            context,
            check => check.VerifyAsync(context, cancellationToken));
        if (!tools.Success)
        {
            return tools;
        }

        return await _compilerStep.VerifyAsync(context, cancellationToken);
    }

    private async Task<StepResult> RunToolChecksAsync(
        SetupContext context,
        Func<ToolCheckStep, Task<StepResult>> runCheck)
    {
        foreach (var check in new[] { _pacmanCheck, _gccCheck, _makeCheck, _gdbCheck })
        {
            var result = await runCheck(check);
            if (!result.Success)
            {
                return result;
            }
        }

        return StepResult.Ok("MSYS2: toolchain UCRT64 detectado.");
    }

    private async Task<StepResult> EnsureToolchainAsync(
        SetupContext context,
        bool installMsys2IfMissing,
        bool refreshPackageDatabases,
        string modeLabel,
        CancellationToken cancellationToken)
    {
        var pacmanWasInstalledByThisRun = false;
        var pacman = await _commandRunner.RunAsync(PacmanPath, "--version", cancellationToken);
        if (!pacman.WasStarted)
        {
            if (!installMsys2IfMissing)
            {
                return StepResult.Missing("MSYS2: pacman no existe; ejecuta instalar o reparar.");
            }

            var msys2 = await _commandRunner.RunAsync(
                "winget",
                "install --exact --id MSYS2.MSYS2 --silent --accept-package-agreements --accept-source-agreements",
                cancellationToken);
            if (!msys2.WasStarted)
            {
                return StepResult.Missing("MSYS2: winget no esta disponible para instalar MSYS2.");
            }

            if (!msys2.IsSuccess)
            {
                return StepResult.Fail($"MSYS2: winget install fallo. {FirstNonEmptyLine(msys2.StandardError)}");
            }

            pacmanWasInstalledByThisRun = true;
        }

        var currentToolchain = await RunToolChecksAsync(
            context,
            check => check.VerifyAsync(context, cancellationToken));
        if (currentToolchain.Success && !refreshPackageDatabases)
        {
            return StepResult.Ok($"MSYS2: toolchain UCRT64 ya estaba listo; no requirio {modeLabel} ni sincronizar pacman.");
        }

        if (refreshPackageDatabases || pacmanWasInstalledByThisRun)
        {
            var update = await _commandRunner.RunAsync(PacmanPath, "-Syu --noconfirm", cancellationToken);
            if (!update.IsSuccess)
            {
                var fallback = await WarningIfToolchainStillVerifiesAsync(
                    context,
                    $"MSYS2: pacman -Syu fallo. {FirstNonEmptyLine(update.StandardError, update.StandardOutput)}",
                    cancellationToken);
                if (fallback is not null)
                {
                    return fallback;
                }

                return StepResult.Fail($"MSYS2: pacman -Syu fallo. {FirstNonEmptyLine(update.StandardError, update.StandardOutput)}");
            }
        }

        var toolchain = await _commandRunner.RunAsync(
            PacmanPath,
            "-S --needed --noconfirm mingw-w64-ucrt-x86_64-toolchain",
            cancellationToken);
        if (!toolchain.IsSuccess)
        {
            var fallback = await WarningIfToolchainStillVerifiesAsync(
                context,
                $"MSYS2: instalacion del toolchain UCRT64 fallo. {FirstNonEmptyLine(toolchain.StandardError, toolchain.StandardOutput)}",
                cancellationToken);
            if (fallback is not null)
            {
                return fallback;
            }

            return StepResult.Fail($"MSYS2: instalacion del toolchain UCRT64 fallo. {FirstNonEmptyLine(toolchain.StandardError, toolchain.StandardOutput)}");
        }

        return StepResult.Ok("MSYS2: toolchain UCRT64 instalado/actualizado.");
    }

    private async Task<StepResult?> WarningIfToolchainStillVerifiesAsync(
        SetupContext context,
        string warningMessage,
        CancellationToken cancellationToken)
    {
        var currentToolchain = await RunToolChecksAsync(
            context,
            check => check.VerifyAsync(context, cancellationToken));
        if (!currentToolchain.Success)
        {
            return null;
        }

        return StepResult.Warning($"{warningMessage} {currentToolchain.Message}");
    }

    private static string FirstNonEmptyLine(params string[] texts)
    {
        foreach (var text in texts)
        {
            using var reader = new StringReader(text);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }
        }

        return string.Empty;
    }
}
