using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public sealed class VsCodeWorkspaceLauncherTests
{
    [Fact]
    public void BuildStartInfo_injects_user_path_alias_and_token()
    {
        var launcher = new VsCodeWorkspaceLauncher(new FakeUserEnvironment(@"C:\msys64\ucrt64\bin;C:\Users\Axel\bin"));

        var startInfo = launcher.BuildStartInfo(TestPaths.WorkspaceRoot, "axel", "token-123");

        Assert.Equal(TestPaths.WorkspaceRoot, startInfo.WorkingDirectory);
        Assert.Contains("ESTUDIO_USUARIO", startInfo.Environment.Keys);
        Assert.Equal("axel", startInfo.Environment["ESTUDIO_USUARIO"]);
        Assert.Equal("token-123", startInfo.Environment[Steps.ExercismCTrackStep.TokenEnvironmentVariable]);
        Assert.Contains(@"C:\msys64\ucrt64\bin", startInfo.Environment["PATH"], StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeUserEnvironment : IUserEnvironment
    {
        private readonly string _path;

        public FakeUserEnvironment(string path)
        {
            _path = path;
        }

        public string? GetUserVariable(string name)
        {
            return name == "PATH" ? _path : null;
        }

        public void SetUserVariable(string name, string value)
        {
        }
    }

    private static class TestPaths
    {
        public static string WorkspaceRoot => Path.Combine(Path.GetTempPath(), "estudio-launch-workspace");
    }
}