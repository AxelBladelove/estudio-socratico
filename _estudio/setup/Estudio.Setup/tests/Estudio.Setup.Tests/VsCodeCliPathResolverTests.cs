using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class VsCodeCliPathResolverTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "EstudioSetupTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveCodeCommand_prefers_local_app_data_code_cmd_when_it_exists()
    {
        var localAppData = Path.Combine(_tempRoot, "Local");
        var codeCmd = Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(codeCmd)!);
        File.WriteAllText(codeCmd, "@echo off");

        var resolved = VsCodeCliPathResolver.ResolveCodeCommand(localAppData, programFiles: "C:\\Missing", programFilesX86: "C:\\MissingX86");

        Assert.Equal(codeCmd, resolved);
    }

    [Fact]
    public void ResolveCodeCommand_falls_back_to_code_when_standard_paths_are_missing()
    {
        var resolved = VsCodeCliPathResolver.ResolveCodeCommand(_tempRoot, programFiles: "C:\\Missing", programFilesX86: "C:\\MissingX86");

        Assert.Equal("code", resolved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
