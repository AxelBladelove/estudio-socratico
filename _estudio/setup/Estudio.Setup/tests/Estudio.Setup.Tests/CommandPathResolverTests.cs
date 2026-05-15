using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public class CommandPathResolverTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "EstudioSetupTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_finds_cmd_file_using_pathext()
    {
        Directory.CreateDirectory(_tempRoot);
        var codeCmd = Path.Combine(_tempRoot, "code.cmd");
        File.WriteAllText(codeCmd, "@echo off");

        var resolved = CommandPathResolver.Resolve("code", _tempRoot, ".EXE;.CMD;.BAT");

        Assert.Equal(codeCmd, resolved, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_keeps_rooted_path_unchanged()
    {
        var rooted = @"C:\msys64\ucrt64\bin\gcc.exe";

        var resolved = CommandPathResolver.Resolve(rooted, _tempRoot, ".EXE;.CMD");

        Assert.Equal(rooted, resolved);
    }

    [Fact]
    public void IsWindowsCommandScript_detects_cmd_and_bat()
    {
        Assert.True(CommandPathResolver.IsWindowsCommandScript(@"C:\bin\code.cmd"));
        Assert.True(CommandPathResolver.IsWindowsCommandScript(@"C:\bin\tool.bat"));
        Assert.False(CommandPathResolver.IsWindowsCommandScript(@"C:\bin\git.exe"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
