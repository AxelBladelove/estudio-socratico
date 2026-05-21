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
        await uninstallManager.UninstallAsync(allowAggressiveCleanup: false, dryRun: false, cancellationToken).ConfigureAwait(false);
        await repairManager.RepairAsync(workspacePath, localAlias, skipGitHub, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UninstallManager(
    AppPaths paths,
    ManifestManager manifestManager,
    LogManager logManager,
    SecurityManager securityManager,
    ICommandRunner? commandRunner = null)
{
    private const string ActionWouldRemove = "wouldRemove";
    private const string ActionRemoved = "removed";
    private const string ActionKept = "kept";
    private const string ActionSkipped = "skipped";

    public Task<UninstallResult> PreviewAsync(bool allowAggressiveCleanup, CancellationToken cancellationToken) =>
        UninstallAsync(allowAggressiveCleanup, dryRun: true, cancellationToken);

    public async Task<UninstallResult> UninstallAsync(
        bool allowAggressiveCleanup,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ManifestPath))
        {
            const string missingManifestMessage = "No existe manifest de instalacion; no se puede atribuir nada al configurador y no se elimino nada.";
            await logManager.WriteAsync("warn", "uninstall", missingManifestMessage, cancellationToken).ConfigureAwait(false);
            return new UninstallResult
            {
                DryRun = dryRun,
                ManifestFound = false,
                SkippedPaths = [paths.LocalAppDataRoot],
                Items =
                [
                    CreateItem(paths.LocalAppDataRoot, ActionSkipped, "Manifest requerido para limpiar herramientas gestionadas.")
                ],
                Message = missingManifestMessage
            };
        }

        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        var wouldRemove = new List<string>();
        var removed = new List<string>();
        var kept = new List<string>();
        var skipped = new List<string>();
        var items = new List<UninstallReportItem>();
        var protectedPaths = BuildProtectedPaths(manifest);

        foreach (var item in protectedPaths)
        {
            kept.Add(item.Path);
            items.Add(item);
        }

        foreach (var packageId in BuildManagedWingetPackages(manifest))
        {
            var label = $"winget:{packageId}";
            if (dryRun)
            {
                wouldRemove.Add(label);
                items.Add(new UninstallReportItem
                {
                    Path = label,
                    Action = ActionWouldRemove,
                    Reason = "Paquete WinGet gestionado por el manifest.",
                    Exists = true
                });
                continue;
            }

            if (commandRunner is null)
            {
                skipped.Add(label);
                items.Add(new UninstallReportItem
                {
                    Path = label,
                    Action = ActionSkipped,
                    Reason = "No hay command runner disponible para ejecutar winget uninstall.",
                    Exists = true
                });
                continue;
            }

            var uninstall = await commandRunner.RunAsync(new CommandSpec
            {
                FileName = "winget",
                Arguments =
                [
                    "uninstall",
                    "--id", packageId,
                    "--exact",
                    "--source", "winget",
                    "--silent"
                ],
                Timeout = TimeSpan.FromMinutes(40),
                AllowNonZeroExitCode = true
            }, cancellationToken).ConfigureAwait(false);
            if (uninstall.Succeeded)
            {
                removed.Add(label);
                items.Add(new UninstallReportItem
                {
                    Path = label,
                    Action = ActionRemoved,
                    Reason = "Paquete WinGet gestionado por el manifest.",
                    Exists = false
                });
            }
            else
            {
                skipped.Add(label);
                items.Add(new UninstallReportItem
                {
                    Path = label,
                    Action = ActionSkipped,
                    Reason = $"winget no pudo desinstalarlo: {uninstall.StandardError}".Trim(),
                    Exists = true
                });
            }
        }

        foreach (var safePath in BuildManagedCandidates(manifest))
        {
            var fullPath = TryGetFullPath(safePath);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                skipped.Add(safePath);
                items.Add(new UninstallReportItem
                {
                    Path = safePath,
                    Action = ActionSkipped,
                    Reason = "Ruta vacia o invalida en el manifest.",
                    Exists = false
                });
                continue;
            }

            if (IsProtected(fullPath, protectedPaths))
            {
                kept.Add(fullPath);
                items.Add(CreateItem(fullPath, ActionKept, "Protegido: datos del estudiante, logs, configuracion local o contenedor base."));
                continue;
            }

            if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
            {
                skipped.Add(fullPath);
                items.Add(CreateItem(fullPath, ActionSkipped, "No existe actualmente; no se toca."));
                continue;
            }

            if (!IsAllowedManagedDeletePath(fullPath, manifest))
            {
                await logManager.WriteAsync("warn", "uninstall", $"Saltando ruta fuera de LocalAppData gestionado: {safePath}", cancellationToken)
                    .ConfigureAwait(false);
                skipped.Add(fullPath);
                items.Add(CreateItem(fullPath, ActionSkipped, "Inseguro: fuera de rutas gestionadas permitidas."));
                continue;
            }

            if (dryRun)
            {
                wouldRemove.Add(fullPath);
                items.Add(CreateItem(fullPath, ActionWouldRemove, "Gestionado por el manifest y dentro de una ruta permitida."));
                continue;
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
            else
            {
                File.Delete(fullPath);
            }

            removed.Add(fullPath);
            items.Add(CreateItem(fullPath, ActionRemoved, "Gestionado por el manifest y dentro de una ruta permitida."));
        }

        var workspaceRemoved = false;
        if (allowAggressiveCleanup && manifest.WorkspacePath is { Length: > 0 } workspace)
        {
            var fullWorkspace = TryGetFullPath(workspace) ?? workspace;
            try
            {
                securityManager.RequireSafeDeleteRoot(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EstudioSocratico"),
                    fullWorkspace);
            }
            catch
            {
                // The workspace remains protected either way; this check only preserves the existing safety audit path.
            }

            kept.Add(fullWorkspace);
            items.Add(CreateItem(fullWorkspace, ActionKept, "Workspace protegido; la limpieza agresiva no borra trabajo del estudiante."));
        }

        var finalMessage = dryRun
            ? "Preview de desinstalacion generado; no se elimino nada."
            : "Desinstalacion segura completada segun manifest.";
        await logManager.WriteAsync("info", "uninstall", finalMessage, cancellationToken)
            .ConfigureAwait(false);
        return new UninstallResult
        {
            DryRun = dryRun,
            ManifestFound = true,
            WouldRemovePaths = wouldRemove.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RemovedPaths = removed,
            KeptPaths = kept.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            SkippedPaths = skipped.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Items = items,
            WorkspaceRemoved = workspaceRemoved,
            Message = finalMessage
        };
    }

    public Task<UninstallResult> UninstallAsync(bool allowAggressiveCleanup, CancellationToken cancellationToken) =>
        UninstallAsync(allowAggressiveCleanup, dryRun: false, cancellationToken);

    private IEnumerable<string> BuildManagedCandidates(InstallerManifest manifest)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in manifest.SafeToRemove)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                result.Add(path);
            }
        }

        foreach (var entry in manifest.Dependencies.Values)
        {
            if (entry.InstalledByEstudio && !string.IsNullOrWhiteSpace(entry.PathAfter))
            {
                result.Add(entry.PathAfter);
            }
        }

        foreach (var extensionId in manifest.VSCodeExtensionsInstalled.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(extensionId, ProductInfo.VSCodeExtensionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extensionsRoot = Path.Combine(paths.UserProfileRoot, ".vscode", "extensions");
            if (Directory.Exists(extensionsRoot))
            {
                foreach (var extensionPath in Directory.EnumerateDirectories(extensionsRoot, $"{extensionId}-*", SearchOption.TopDirectoryOnly))
                {
                    result.Add(extensionPath);
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> BuildManagedWingetPackages(InstallerManifest manifest)
    {
        foreach (var entry in manifest.Dependencies.Values)
        {
            if (!entry.InstalledByEstudio ||
                !string.Equals(entry.InstallerSource, "winget", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var packageId = entry.Id switch
            {
                DependencyId.NodeJs => "OpenJS.NodeJS.LTS",
                DependencyId.Python => "Python.Python.3.13",
                DependencyId.Git => "Git.Git",
                DependencyId.GitHubCli => "GitHub.cli",
                DependencyId.ExercismCli => "Exercism.CLI",
                DependencyId.VSCode => "Microsoft.VisualStudioCode",
                DependencyId.Msys2 => "MSYS2.MSYS2",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(packageId))
            {
                yield return packageId;
            }
        }
    }

    private List<UninstallReportItem> BuildProtectedPaths(InstallerManifest manifest)
    {
        var result = new List<UninstallReportItem>
        {
            CreateItem(paths.LocalAppDataRoot, ActionKept, "Contenedor base de datos locales del configurador."),
            CreateItem(paths.LogsRoot, ActionKept, "Logs protegidos para diagnostico."),
            CreateItem(paths.ManifestPath, ActionKept, "Manifest protegido para auditoria y reparacion.")
        };

        AddProtectedWorkspacePath(result, manifest.WorkspacePath, "Workspace del estudiante protegido.");
        AddProtectedWorkspacePath(result, CombineIfKnown(manifest.WorkspacePath, "Ejercicios"), "Ejercicios del estudiante protegidos.");
        AddProtectedWorkspacePath(result, CombineIfKnown(manifest.WorkspacePath, "usuario"), "Carpeta usuario protegida.");
        AddProtectedWorkspacePath(result, CombineIfKnown(manifest.WorkspacePath, "usuario", "logs"), "Logs del estudiante protegidos.");
        AddProtectedWorkspacePath(result, CombineIfKnown(manifest.WorkspacePath, "usuario", "config"), "Configuracion local del estudiante protegida.");
        AddProtectedWorkspacePath(
            result,
            CombineIfKnown(manifest.WorkspacePath, "usuario", "config", "estudio-socratico.extension.local.json"),
            "API key local protegida.");

        if (!string.IsNullOrWhiteSpace(paths.RepoRoot))
        {
            AddProtectedWorkspacePath(result, paths.RepoRoot, "Repositorio fuente protegido.");
        }

        return result;
    }

    private static void AddProtectedWorkspacePath(List<UninstallReportItem> result, string? path, string reason)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        result.Add(CreateItem(path, ActionKept, reason));
    }

    private static string? CombineIfKnown(string? root, params string[] parts) =>
        string.IsNullOrWhiteSpace(root) ? null : Path.Combine([root, .. parts]);

    private static bool IsProtected(string fullPath, IReadOnlyList<UninstallReportItem> protectedPaths)
    {
        foreach (var item in protectedPaths)
        {
            var protectedPath = TryGetFullPath(item.Path);
            if (string.IsNullOrWhiteSpace(protectedPath))
            {
                continue;
            }

            if (string.Equals(fullPath, protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var exactOnly = item.Reason.Contains("Contenedor base", StringComparison.OrdinalIgnoreCase) ||
                            item.Reason.Contains("Manifest", StringComparison.OrdinalIgnoreCase);
            if (!exactOnly && PathSafety.IsInside(protectedPath, fullPath))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAllowedManagedDeletePath(string fullPath, InstallerManifest manifest)
    {
        if (PathSafety.IsInside(paths.LocalAppDataRoot, fullPath))
        {
            return true;
        }

        var extensionsRoot = Path.Combine(paths.UserProfileRoot, ".vscode", "extensions");
        if (manifest.VSCodeExtensionsInstalled.Contains(ProductInfo.VSCodeExtensionId, StringComparer.OrdinalIgnoreCase) &&
            PathSafety.IsInside(extensionsRoot, fullPath) &&
            Path.GetFileName(fullPath).StartsWith(ProductInfo.VSCodeExtensionId + "-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return manifest.Dependencies.TryGetValue(DependencyId.Msys2, out var msys2) &&
               msys2.InstalledByEstudio &&
               string.Equals(Path.GetFullPath(ProductInfo.DefaultMsys2Root), fullPath, StringComparison.OrdinalIgnoreCase);
    }

    private static UninstallReportItem CreateItem(string path, string action, string reason)
    {
        var fullPath = TryGetFullPath(path) ?? path;
        return new UninstallReportItem
        {
            Path = fullPath,
            Action = action,
            Reason = reason,
            Exists = Directory.Exists(fullPath) || File.Exists(fullPath)
        };
    }

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }
}
