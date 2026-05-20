using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class DiagnosticsManager(
    DependencyDetector detector,
    SystemProbe probe,
    LogManager logManager)
{
    public async Task<DiagnosticsReport> RunAsync(string? workspacePath, CancellationToken cancellationToken)
    {
        var dependencies = await detector.DetectAllAsync(cancellationToken).ConfigureAwait(false);
        var report = new DiagnosticsReport
        {
            WorkspacePath = workspacePath,
            Dependencies = dependencies,
            Environment = probe.SnapshotEnvironment()
        };
        await logManager.WriteDiagnosticsAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }
}

public sealed class RepairManager(
    DependencyInstaller dependencyInstaller,
    WorkspaceManager workspaceManager,
    VSCodeManager vsCodeManager,
    GitHubAccountManager gitHubAccountManager,
    LogManager logManager)
{
    public async Task RepairAsync(string workspacePath, string localAlias, bool skipGitHub, CancellationToken cancellationToken)
    {
        foreach (var requirement in DependencyDetector.Requirements.Where(x => x.Required))
        {
            await dependencyInstaller.EnsureAsync(requirement, cancellationToken).ConfigureAwait(false);
        }

        workspaceManager.RequireWorkspaceShape(workspacePath);
        await workspaceManager.PrepareAsync(workspacePath, localAlias, cancellationToken).ConfigureAwait(false);
        await vsCodeManager.PrepareAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        if (!skipGitHub)
        {
            await gitHubAccountManager.ConfigureRepositoryAsync(workspacePath, localAlias, cancellationToken)
                .ConfigureAwait(false);
        }

        await logManager.WriteAsync("info", "repair", "Reparacion completada.", cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ReinstallManager(RepairManager repairManager, UninstallManager uninstallManager)
{
    public async Task ReinstallAsync(string workspacePath, string localAlias, bool skipGitHub, CancellationToken cancellationToken)
    {
        await uninstallManager.UninstallAsync(allowAggressiveCleanup: false, cancellationToken).ConfigureAwait(false);
        await repairManager.RepairAsync(workspacePath, localAlias, skipGitHub, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UninstallManager(AppPaths paths, ManifestManager manifestManager, LogManager logManager, SecurityManager securityManager)
{
    public async Task UninstallAsync(bool allowAggressiveCleanup, CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ManifestPath))
        {
            throw new InvalidOperationException("No existe manifest de instalacion; no se puede limpiar con seguridad.");
        }

        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        foreach (var safePath in manifest.SafeToRemove.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(safePath) || !Directory.Exists(safePath) && !File.Exists(safePath))
            {
                continue;
            }

            var allowedRoot = paths.LocalAppDataRoot;
            if (!PathSafety.IsInside(allowedRoot, safePath))
            {
                await logManager.WriteAsync("warn", "uninstall", $"Saltando ruta fuera de LocalAppData gestionado: {safePath}", cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (Directory.Exists(safePath))
            {
                Directory.Delete(safePath, recursive: true);
            }
            else
            {
                File.Delete(safePath);
            }
        }

        if (allowAggressiveCleanup && manifest.WorkspacePath is { Length: > 0 } workspace)
        {
            securityManager.RequireSafeDeleteRoot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EstudioSocratico"), workspace);
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }

        await logManager.WriteAsync("info", "uninstall", "Desinstalacion segura completada segun manifest.", cancellationToken)
            .ConfigureAwait(false);
    }
}
