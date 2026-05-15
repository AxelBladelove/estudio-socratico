using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class CCompilerStep : ISetupStep
{
    private const string ExpectedOutput = "Estudio Socratico GCC OK";
    private readonly ICommandRunner _commandRunner;
    private readonly string _workRoot;
    private readonly ToolCheckStep _toolCheck;

    public CCompilerStep(ICommandRunner commandRunner, string? workRoot = null, string compilerFileName = "gcc")
    {
        _commandRunner = commandRunner;
        _workRoot = workRoot ?? Path.Combine(Path.GetTempPath(), "EstudioSocratico", "setup-compiler-checks");
        _toolCheck = new ToolCheckStep(Id, Name, compilerFileName, "--version", commandRunner);
        CompilerFileName = compilerFileName;
    }

    public string Id => "gcc";
    public string Name => "GCC";
    public string CompilerFileName { get; }

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return _toolCheck.DetectAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepResult.Fail("GCC: instalacion via MSYS2 pendiente de implementar."));
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepResult.Fail("GCC: actualizacion via MSYS2 pendiente de implementar."));
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepResult.Fail("GCC: reparacion via MSYS2 pendiente de implementar."));
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var workDir = Path.Combine(_workRoot, Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(workDir, "hello_world.c");
        var outputPath = Path.Combine(workDir, "hello_world.exe");

        try
        {
            Directory.CreateDirectory(workDir);
            await File.WriteAllTextAsync(
                sourcePath,
                """
                #include <stdio.h>

                int main(void) {
                    printf("Estudio Socratico GCC OK\n");
                    return 0;
                }
                """,
                cancellationToken);

            var compile = await _commandRunner.RunAsync(
                CompilerFileName,
                $"{Quote(sourcePath)} -o {Quote(outputPath)}",
                cancellationToken);
            if (!compile.WasStarted)
            {
                return StepResult.Missing("GCC no esta disponible para compilar.");
            }

            if (!compile.IsSuccess)
            {
                return StepResult.Fail($"GCC: fallo la compilacion. {FirstNonEmptyLine(compile.StandardError)}");
            }

            var run = await _commandRunner.RunAsync(outputPath, string.Empty, cancellationToken);
            if (!run.IsSuccess)
            {
                return StepResult.Fail($"GCC: el ejecutable compilado no corrio. {FirstNonEmptyLine(run.StandardError)}");
            }

            if (!run.StandardOutput.Contains(ExpectedOutput, StringComparison.Ordinal))
            {
                return StepResult.Fail("GCC: el ejecutable no produjo la salida esperada.");
            }

            return StepResult.Ok(ExpectedOutput);
        }
        finally
        {
            TryDelete(workDir);
        }
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

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
