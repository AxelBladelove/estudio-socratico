using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public class DiagnosisTests
{
    [Fact]
    public async Task DiagnoseAsync_WritesDiagnosticsFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"estudio-diagnose-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var paths = new AppPaths(
                repoRoot: root,
                localAppDataRoot: Path.Combine(root, "LocalAppData"));
            var engine = new ConfiguratorEngine(paths, new AlwaysFailingCommandRunner());

            var snapshot = await engine.DiagnoseAsync(root);

            Assert.NotNull(snapshot);
            Assert.True(File.Exists(engine.Logs.DiagnosticsPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class AlwaysFailingCommandRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommandResult
            {
                Spec = spec,
                ExitCode = 1,
                StandardError = $"{spec.FileName} unavailable",
                Duration = TimeSpan.Zero
            });
    }
}
