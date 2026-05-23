using System.Text.Json;
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

    [Fact]
    public async Task Diagnostics_ReportsActualInstalledVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"estudio-diagnose-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var paths = new AppPaths(
                repoRoot: root,
                localAppDataRoot: Path.Combine(root, "LocalAppData"));
            var engine = new ConfiguratorEngine(paths, new AlwaysFailingCommandRunner());

            _ = await engine.DiagnoseAsync(root);

            var json = await File.ReadAllTextAsync(engine.Logs.DiagnosticsPath);
            var report = JsonSerializer.Deserialize<DiagnosticsReport>(json, JsonDefaults.Options);
            Assert.NotNull(report);
            Assert.Equal(ProductInfo.PublicDisplayVersion, report!.PublicDisplayVersion);
            Assert.Equal(ProductInfo.Version, report.InternalPackageVersion);
            Assert.Equal(ProductInfo.Version, report.ConfiguratorVersion);
            Assert.False(string.IsNullOrWhiteSpace(report.InstalledBuild));
            Assert.False(string.IsNullOrWhiteSpace(report.Source));
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
