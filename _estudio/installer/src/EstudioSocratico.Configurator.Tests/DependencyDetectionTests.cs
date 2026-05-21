using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class DependencyDetectionTests
{
    [Fact]
    public async Task Detects_Command_With_Version()
    {
        var temp = Path.Combine(Path.GetTempPath(), "estudio-node-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var nodePath = Path.Combine(temp, "node.exe");
        File.WriteAllText(nodePath, "");
        var runner = new FakeRunner(spec =>
        {
            if (spec.FileName == "where.exe")
            {
                return FakeRunner.Result(spec, 0, nodePath + "\r\n");
            }

            return FakeRunner.Result(spec, 0, "v22.16.0");
        });
        var detector = new DependencyDetector(runner);
        var state = await detector.DetectAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.NodeJs));

        Assert.Equal(DependencyStatus.Ready, state.Status);
        Assert.Equal("22.16.0", state.Version);
    }

    [Fact]
    public async Task Builds_Winget_Install_Command_With_Agreements()
    {
        CommandSpec? captured = null;
        var runner = new FakeRunner(spec =>
        {
            captured = spec;
            return FakeRunner.Result(spec, 0, "ok");
        });
        var broker = new WingetBroker(runner, new DependencyDetector(runner), new LogManager(new AppPaths(localAppDataRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))));

        await broker.InstallPackageAsync("Git.Git", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Contains("--accept-package-agreements", captured!.Arguments);
        Assert.Contains("--accept-source-agreements", captured.Arguments);
        Assert.Contains("Git.Git", captured.Arguments);
    }

    [Fact]
    public async Task PythonDetection_Ignores_WindowsApps_Alias_When_Real_Install_Exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-python-" + Guid.NewGuid().ToString("N"));
        var windowsApps = Path.Combine(root, "WindowsApps");
        var pythonRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Python",
            "Python313-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(windowsApps);
        Directory.CreateDirectory(pythonRoot);
        var alias = Path.Combine(windowsApps, "python.exe");
        var realPython = Path.Combine(pythonRoot, "python.exe");
        File.WriteAllText(alias, "");
        File.WriteAllText(realPython, "");
        try
        {
            var runner = new FakeRunner(spec =>
            {
                if (spec.FileName == "where.exe")
                {
                    return FakeRunner.Result(spec, 0, alias + "\r\n");
                }

                if (string.Equals(spec.FileName, alias, StringComparison.OrdinalIgnoreCase))
                {
                    return FakeRunner.Result(spec, 9009, "", "no se encontró Python; ejecutar sin argumentos para instalar desde el Microsoft Store.");
                }

                if (string.Equals(spec.FileName, realPython, StringComparison.OrdinalIgnoreCase))
                {
                    return FakeRunner.Result(spec, 0, "Python 3.13.3");
                }

                return FakeRunner.Result(spec, 1);
            });

            var detector = new DependencyDetector(runner);
            var state = await detector.DetectAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.Python));

            Assert.Equal(DependencyStatus.Ready, state.Status);
            Assert.Equal(realPython, state.Path);
            Assert.Equal("3.13.3", state.Version);
        }
        finally
        {
            if (Directory.Exists(pythonRoot))
            {
                Directory.Delete(pythonRoot, recursive: true);
            }
        }
    }
}

internal sealed class FakeRunner(Func<CommandSpec, CommandResult> handler) : ICommandRunner
{
    public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(handler(spec));
    }

    public static CommandResult Result(CommandSpec spec, int exitCode, string output = "", string error = "")
    {
        return new CommandResult
        {
            Spec = spec,
            ExitCode = exitCode,
            StandardOutput = output,
            StandardError = error,
            Duration = TimeSpan.FromMilliseconds(1)
        };
    }
}
