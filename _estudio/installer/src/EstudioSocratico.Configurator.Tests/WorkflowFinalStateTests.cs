using System.Reflection;
using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class WorkflowFinalStateTests
{
    [Fact]
    public async Task ResolveAlias_UsesLocalAliasBeforeEnvironmentUser()
    {
        var workspace = CreateMinimalWorkspace();
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(workspace, "LocalAppData"));
        var engine = new ConfiguratorEngine(paths, new PassiveRunner());
        var request = new SetupRequest { LocalAlias = "Ana Maria" };
        var method = typeof(ConfiguratorEngine).GetMethod("ResolveAliasAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        var task = Assert.IsType<Task<string>>(method!.Invoke(engine, [request, CancellationToken.None]));
        var alias = await task;

        Assert.Equal("ana-maria", alias);
        Assert.NotEqual(LocalAliasNormalizer.Normalize(Environment.UserName), alias);
    }

    [Fact]
    public async Task ApplyWorkflow_RequeriesStateBeforeFinalScreen()
    {
        var workspace = CreateMinimalWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, ".git"));
        var paths = new AppPaths(repoRoot: workspace, localAppDataRoot: Path.Combine(workspace, "LocalAppData"));
        var manifestManager = new ManifestManager(paths);
        await manifestManager.SaveAsync(new InstallerManifest
        {
            WorkspacePath = workspace,
            LocalAlias = "axel",
            GitHub = new AccountState { Configured = true, UserName = "AxelBladelove" },
            Exercism = new AccountState { Configured = true, UserName = "learner" }
        });

        var engine = new ConfiguratorEngine(paths, new PassiveRunner());

        var summary = await engine.RunSmokeTestAsync(workspace);

        Assert.NotNull(summary.CurrentState);
        Assert.True(summary.CurrentState!.BuildFlowValid);
        Assert.Equal("passed", summary.CurrentState.FinalReadiness.SmokeTestStatus);
    }

    private static string CreateMinimalWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-final-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "soporte", "exercism", "catalogs"));
        Directory.CreateDirectory(Path.Combine(root, "_estudio", "include"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# test");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "build.cmd"), "@echo off");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "scripts", "compilar_y_grabar.bat"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "exercism", "manager.ps1"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "soporte", "exercism", "catalogs", "alejandro.json"), "https://gist.github.com/example");
        File.WriteAllText(Path.Combine(root, "_estudio", "include", "conio.h"), "");
        File.WriteAllText(Path.Combine(root, "_estudio", "errores.template.md"), "# Errores");
        return root;
    }

    private sealed class PassiveRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            if (spec.FileName.EndsWith("build.cmd", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new CommandResult
                {
                    Spec = spec,
                    ExitCode = 0,
                    StandardOutput = "ok",
                    Duration = TimeSpan.FromMilliseconds(1)
                });
            }

            if (string.Equals(spec.FileName, "where.exe", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new CommandResult
                {
                    Spec = spec,
                    ExitCode = 1,
                    Duration = TimeSpan.FromMilliseconds(1)
                });
            }

            return Task.FromResult(new CommandResult
            {
                Spec = spec,
                ExitCode = 0,
                StandardOutput = "ok",
                Duration = TimeSpan.FromMilliseconds(1)
            });
        }
    }
}
