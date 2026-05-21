using System.Text.Json;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class ConfiguratorEngine
{
    private readonly AppPaths _paths;
    private readonly LogManager _logManager;
    private readonly ManifestManager _manifestManager;
    private readonly DependencyDetector _detector;
    private readonly DependencyInstaller _dependencyInstaller;
    private readonly GitHubAccountManager _gitHubAccountManager;
    private readonly ExercismManager _exercismManager;
    private readonly WorkspaceManager _workspaceManager;
    private readonly VSCodeManager _vsCodeManager;
    private readonly DiagnosticsManager _diagnosticsManager;
    private readonly RepairManager _repairManager;
    private readonly ReinstallManager _reinstallManager;
    private readonly UninstallManager _uninstallManager;
    private readonly TelemetryCompatibilityManager _telemetryCompatibilityManager;
    private readonly GistImporterManager _gistImporterManager;
    private readonly SetupPlanner _planner;

    public ConfiguratorEngine(AppPaths? paths = null, ICommandRunner? commandRunner = null)
    {
        _paths = paths ?? new AppPaths();
        _logManager = new LogManager(_paths);
        _manifestManager = new ManifestManager(_paths);
        var runner = commandRunner ?? new ProcessCommandRunner(_logManager);
        _detector = new DependencyDetector(runner, managedToolsDirectory: Path.Combine(_paths.ToolsRoot, "bin"));
        var probe = new SystemProbe(_paths);
        var pathManager = new PathManager(_paths, _logManager);
        var winget = new WingetBroker(runner, _detector, _logManager);
        var fallback = new OfficialInstallerFallback(_logManager);
        var download = new DownloadManager(_paths, _logManager);
        var checksum = new ChecksumVerifier();
        var elevated = new ElevatedWorkerClient(_logManager);
        _dependencyInstaller = new DependencyInstaller(
            _paths,
            _detector,
            winget,
            fallback,
            download,
            checksum,
            pathManager,
            _manifestManager,
            elevated,
            _logManager,
            probe);
        _gitHubAccountManager = new GitHubAccountManager(runner, _manifestManager, _logManager);
        _exercismManager = new ExercismManager(runner, _manifestManager, _paths, _logManager);
        _workspaceManager = new WorkspaceManager(_paths, _manifestManager, _logManager);
        var extensionManager = new ExtensionManager(_paths, _logManager);
        _vsCodeManager = new VSCodeManager(runner, extensionManager, _manifestManager, _logManager);
        _diagnosticsManager = new DiagnosticsManager(_detector, probe, _logManager);
        var security = new SecurityManager();
        _uninstallManager = new UninstallManager(_paths, _manifestManager, _logManager, security, runner);
        _repairManager = new RepairManager(_dependencyInstaller, _workspaceManager, _vsCodeManager, _gitHubAccountManager, _logManager);
        _reinstallManager = new ReinstallManager(_repairManager, _uninstallManager);
        _telemetryCompatibilityManager = new TelemetryCompatibilityManager(runner, _logManager);
        _gistImporterManager = new GistImporterManager();
        _planner = new SetupPlanner();
    }

    public LogManager Logs => _logManager;
    public ManifestManager Manifest => _manifestManager;
    public VSCodeManager VSCode => _vsCodeManager;
    public SetupPlanner Planner => _planner;

    public Task<UninstallResult> PreviewUninstallAsync(
        bool allowAggressiveCleanup = false,
        CancellationToken cancellationToken = default) =>
        _uninstallManager.PreviewAsync(allowAggressiveCleanup, cancellationToken);

    public async Task<IReadOnlyList<DependencyState>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await _logManager.StartRunAsync(cancellationToken).ConfigureAwait(false);
        return await _detector.DetectAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Full diagnosis: dependencies + auth + workspace + build flow.
    /// Returns a UIStateSnapshot for the React UI.
    /// </summary>
    public async Task<UIStateSnapshot> DiagnoseAsync(
        string? workspacePath = null,
        string? localAlias = null,
        CancellationToken cancellationToken = default)
    {
        await _logManager.StartRunAsync(cancellationToken).ConfigureAwait(false);
        var (snapshot, _) = await BuildCurrentStateAsync(workspacePath, localAlias, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    /// <summary>
    /// Create a setup plan from current diagnosis without applying changes.
    /// </summary>
    public async Task<SetupPlan> CreatePlanAsync(CancellationToken cancellationToken = default)
    {
        var dependencies = await _detector.DetectAllAsync(cancellationToken).ConfigureAwait(false);
        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        var alias = LocalAliasNormalizer.Normalize(manifest.LocalAlias, Environment.UserName);
        var workspace = manifest.WorkspacePath ?? _paths.GetRecommendedWorkspacePath(alias);
        var workspaceValid = Directory.Exists(workspace) && Directory.Exists(Path.Combine(workspace, ".git"));
        var buildFlowValid = manifest.BuildFlowValidated && workspaceValid;

        return _planner.CreatePlan(dependencies, manifest.GitHub, manifest.Exercism, workspaceValid, buildFlowValid);
    }

    public async Task<SetupSummary> RunAsync(
        SetupRequest request,
        IProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        var progress = new CompositeProgressSink([_logManager, progressSink ?? NullProgressSink.Instance]);
        var errors = new List<InstallerError>();
        var states = new List<DependencyState>();
        var workspace = request.WorkspacePath;
        UninstallResult? uninstallReport = null;

        await _logManager.StartRunAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            switch (request.Mode)
            {
                case SetupMode.Diagnostics:
                    await progress.ReportAsync(new ProgressEvent
                    {
                        StepId = "diagnostics",
                        Title = "Diagnostico",
                        Message = "Analizando entorno.",
                        Percent = 10,
                        Status = DependencyStatus.Installing
                    }, cancellationToken)
                        .ConfigureAwait(false);
                    var report = await _diagnosticsManager.RunAsync(workspace, cancellationToken).ConfigureAwait(false);
                    states.AddRange(report.Dependencies);
                    await progress.ReportAsync(new ProgressEvent
                    {
                        StepId = "diagnostics",
                        Title = "Diagnostico",
                        Message = "Diagnostico completado.",
                        Percent = 100,
                        Status = DependencyStatus.Ready
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case SetupMode.Uninstall:
                    await progress.ReportAsync(new ProgressEvent
                    {
                        StepId = "uninstall",
                        Title = "Desinstalacion",
                        Message = "Leyendo manifest y rutas gestionadas.",
                        Percent = 10,
                        Status = DependencyStatus.Installing
                    }, cancellationToken)
                        .ConfigureAwait(false);
                    var cleanup = await _uninstallManager.UninstallAsync(
                        request.AllowAggressiveCleanup,
                        request.UninstallDryRun,
                        cancellationToken).ConfigureAwait(false);
                    uninstallReport = cleanup;
                    await progress.ReportAsync(new ProgressEvent
                    {
                        StepId = "uninstall",
                        Title = "Desinstalacion",
                        Message = cleanup.Message,
                        Percent = 100,
                        Status = DependencyStatus.Ready
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case SetupMode.Update:
                    workspace = await UpdateAsync(request, progress, states, cancellationToken).ConfigureAwait(false);
                    break;

                case SetupMode.Reinstall:
                    workspace = await ReinstallAsync(request, progress, cancellationToken).ConfigureAwait(false);
                    break;

                case SetupMode.Repair:
                    workspace = await RepairAsync(request, progress, cancellationToken).ConfigureAwait(false);
                    break;

                case SetupMode.Install:
                default:
                    workspace = await InstallAsync(request, progress, states, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            var error = NormalizeError(ex);
            errors.Add(error);
            await _logManager.WriteErrorAsync(error, cancellationToken).ConfigureAwait(false);
        }

        if (request.Mode is SetupMode.Install or SetupMode.Update or SetupMode.Repair or SetupMode.Reinstall)
        {
            await PersistBuildFlowValidationAsync(workspace, errors.Count == 0, cancellationToken).ConfigureAwait(false);
        }

        var (currentState, finalDependencies) = await BuildCurrentStateAsync(workspace, null, cancellationToken).ConfigureAwait(false);
        await LogFinalReadinessCheckAsync(currentState, cancellationToken).ConfigureAwait(false);
        var globalState = errors.Count > 0 ? GlobalState.Failed : currentState.GlobalState;
        var globalMessage = errors.Count > 0
            ? errors[0].Description
            : currentState.GlobalMessage;

        var succeeded = request.Mode switch
        {
            SetupMode.Diagnostics or SetupMode.Uninstall => errors.Count == 0,
            _ => errors.Count == 0 && currentState.GlobalState == GlobalState.ReadyToStudy
        };

        return new SetupSummary
        {
            Mode = request.Mode,
            Succeeded = succeeded,
            GlobalState = globalState,
            GlobalMessage = globalMessage,
            Dependencies = finalDependencies,
            Errors = errors,
            WorkspacePath = workspace,
            LogPath = _logManager.InstallerLogPath,
            DiagnosticsPath = _logManager.DiagnosticsPath,
            CurrentState = currentState,
            UninstallReport = uninstallReport
        };
    }

    public Task<AccountState> SwitchGitHubAccountAsync(CancellationToken cancellationToken = default)
    {
        return ConfigureGitHubAsync(switchAccount: true, cancellationToken: cancellationToken);
    }

    public async Task<AccountState> ConfigureGitHubAsync(
        bool switchAccount = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var account = await _gitHubAccountManager.EnsureLoginAsync(switchAccount, cancellationToken).ConfigureAwait(false);
        var workspace = await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        if (File.Exists(Path.Combine(workspace, "AGENTS.md")))
        {
            var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
            var alias = LocalAliasNormalizer.Normalize(manifest.LocalAlias, Environment.UserName);
            await _gitHubAccountManager.ConfigureRepositoryAsync(workspace, alias, cancellationToken)
                .ConfigureAwait(false);
        }

        return account;
    }

    public async Task<AccountState> ConfigureExercismAsync(string token, string? workspacePath, CancellationToken cancellationToken = default)
    {
        var workspace = await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        return await _exercismManager.ConfigureTokenAsync(token, workspace, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetKnownWorkspacePathAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        return await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReinstallVSCodeExtensionAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var workspace = await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await _vsCodeManager.RepairLocalExtensionAsync(workspace, cancellationToken).ConfigureAwait(false);
    }

    public async Task OpenExercisePanelAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var workspace = await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await _vsCodeManager.OpenExercisePanelAsync(workspace, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExtensionApiKeyConfigState> EnsureExtensionApiKeyConfigAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var workspace = await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        return await _workspaceManager.EnsureExtensionApiKeyConfigAsync(workspace, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExtensionApiKeyConfigState> GetExtensionApiKeyConfigAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var workspace = await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        return _workspaceManager.DescribeExtensionApiKeyConfig(workspace);
    }

    public async Task<SetupSummary> RunSmokeTestAsync(
        string? workspacePath = null,
        IProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        var progress = new CompositeProgressSink([_logManager, progressSink ?? NullProgressSink.Instance]);
        var errors = new List<InstallerError>();
        var workspace = await ResolveKnownWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);

        await _logManager.StartRunAsync(cancellationToken).ConfigureAwait(false);
        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "smoke-test",
            Title = "Validacion F9",
            Message = "Ejecutando build.cmd sin commits automaticos.",
            Percent = 20,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        try
        {
            await _telemetryCompatibilityManager.ValidateBuildFlowAsync(workspace, cancellationToken).ConfigureAwait(false);
            await progress.ReportAsync(new ProgressEvent
            {
                StepId = "smoke-test",
                Title = "Validacion F9",
                Message = "Smoke test completado sin commits automaticos.",
                Percent = 100,
                Status = DependencyStatus.Ready
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = NormalizeError(ex);
            errors.Add(error);
            await _logManager.WriteErrorAsync(error, cancellationToken).ConfigureAwait(false);
            await progress.ReportAsync(new ProgressEvent
            {
                StepId = "smoke-test",
                Title = "Validacion F9",
                Message = error.Description,
                Percent = 100,
                Status = DependencyStatus.Failed
            }, cancellationToken).ConfigureAwait(false);
        }

        await PersistBuildFlowValidationAsync(workspace, errors.Count == 0, cancellationToken).ConfigureAwait(false);
        var (currentState, finalDependencies) = await BuildCurrentStateAsync(workspace, null, cancellationToken).ConfigureAwait(false);
        await LogFinalReadinessCheckAsync(currentState, cancellationToken).ConfigureAwait(false);
        var globalState = errors.Count > 0 ? GlobalState.Failed : currentState.GlobalState;

        return new SetupSummary
        {
            Mode = SetupMode.Diagnostics,
            Succeeded = errors.Count == 0,
            GlobalState = globalState,
            GlobalMessage = errors.Count == 0
                ? "Smoke test F9 completado sin commits automaticos."
                : errors[0].Description,
            Dependencies = finalDependencies,
            Errors = errors,
            WorkspacePath = workspace,
            LogPath = _logManager.InstallerLogPath,
            DiagnosticsPath = _logManager.DiagnosticsPath,
            CurrentState = currentState
        };
    }

    private async Task<string> InstallAsync(
        SetupRequest request,
        IProgressSink progress,
        List<DependencyState> states,
        CancellationToken cancellationToken)
    {
        var requirements = DependencyDetector.Requirements.Where(x => x.Required).ToArray();
        for (var i = 0; i < requirements.Length; i++)
        {
            var requirement = requirements[i];
            await progress.ReportAsync(new ProgressEvent
            {
                StepId = requirement.Id.ToString(),
                Title = requirement.DisplayName,
                Message = "Detectando, instalando y validando.",
                Percent = 10 + (i * 55.0 / requirements.Length),
                Dependency = requirement.Id,
                Status = DependencyStatus.Installing
            }, cancellationToken).ConfigureAwait(false);

            var state = await _dependencyInstaller.EnsureAsync(requirement, cancellationToken).ConfigureAwait(false);
            states.Add(state);
        }

        var alias = await ResolveAliasAsync(request, cancellationToken).ConfigureAwait(false);
        var target = request.WorkspacePath ?? _paths.GetRecommendedWorkspacePath(alias);
        await progress.ReportAsync(new ProgressEvent { StepId = "github", Title = "GitHub", Message = "Preparando fork y workspace.", Percent = 70 }, cancellationToken)
            .ConfigureAwait(false);
        var workspace = await _gitHubAccountManager.EnsureWorkspaceRepositoryAsync(target, alias, request.SkipGitHubLogin, cancellationToken)
            .ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent { StepId = "workspace", Title = "Workspace", Message = "Configurando carpeta de estudio.", Percent = 78 }, cancellationToken)
            .ConfigureAwait(false);
        workspace = await _workspaceManager.PrepareAsync(workspace, alias, cancellationToken).ConfigureAwait(false);

        if (!request.SkipExercism && !string.IsNullOrWhiteSpace(request.ExercismToken))
        {
            await progress.ReportAsync(new ProgressEvent { StepId = "exercism", Title = "Exercism", Message = "Configurando token de forma segura.", Percent = 84 }, cancellationToken)
                .ConfigureAwait(false);
            await _exercismManager.ConfigureTokenAsync(request.ExercismToken, workspace, cancellationToken).ConfigureAwait(false);
        }
        else if (!request.SkipExercism)
        {
            await progress.ReportAsync(new ProgressEvent
            {
                StepId = "exercism",
                Title = "Exercism",
                Message = "Falta token de Exercism para completar el flujo.",
                Percent = 84,
                Status = DependencyStatus.NeedsUserAction
            }, cancellationToken).ConfigureAwait(false);
        }

        await progress.ReportAsync(new ProgressEvent { StepId = "vscode", Title = "VS Code", Message = "Aplicando perfil del workspace.", Percent = 90 }, cancellationToken)
            .ConfigureAwait(false);
        await _vsCodeManager.PrepareAsync(workspace, cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent { StepId = "compat", Title = "Compatibilidad", Message = "Validando Gists y telemetria.", Percent = 96 }, cancellationToken)
            .ConfigureAwait(false);
        _gistImporterManager.ValidateGistCatalogs(workspace);
        await _telemetryCompatibilityManager.ValidateBuildFlowAsync(workspace, cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent { StepId = "complete", Title = "Listo", Message = "Estudio Socratico esta listo.", Percent = 100 }, cancellationToken)
            .ConfigureAwait(false);
        return workspace;
    }

    private async Task<string> RepairAsync(
        SetupRequest request,
        IProgressSink progress,
        CancellationToken cancellationToken)
    {
        var workspace = await ResolveWorkspaceAsync(request, cancellationToken).ConfigureAwait(false);
        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "repair",
            Title = "Reparacion",
            Message = "Reinstalando o reparando herramientas requeridas.",
            Percent = 20,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        await _repairManager.RepairAsync(workspace, await ResolveAliasAsync(request, cancellationToken).ConfigureAwait(false), request.SkipGitHubLogin, cancellationToken)
            .ConfigureAwait(false);

        await RevalidateExercismAsync(request, workspace, progress, 76, "exercism", cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "smoke-test",
            Title = "Validacion F9",
            Message = "Ejecutando smoke test sin commits automaticos.",
            Percent = 90,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);
        await _telemetryCompatibilityManager.ValidateBuildFlowAsync(workspace, cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "repair",
            Title = "Reparacion",
            Message = "Reparacion completada.",
            Percent = 100,
            Status = DependencyStatus.Ready
        }, cancellationToken).ConfigureAwait(false);
        return workspace;
    }

    private async Task<string> UpdateAsync(
        SetupRequest request,
        IProgressSink progress,
        List<DependencyState> states,
        CancellationToken cancellationToken)
    {
        var workspace = await ResolveWorkspaceAsync(request, cancellationToken).ConfigureAwait(false);
        var requirements = DependencyDetector.Requirements.Where(x => x.Required).ToArray();
        for (var i = 0; i < requirements.Length; i++)
        {
            var requirement = requirements[i];
            await progress.ReportAsync(new ProgressEvent
            {
                StepId = $"update-{requirement.Id}",
                Title = $"Actualizar {requirement.DisplayName}",
                Message = "Instalando faltantes, actualizando obsoletos y reparando errores conocidos.",
                Percent = 15 + (i * 45.0 / requirements.Length),
                Dependency = requirement.Id,
                Status = DependencyStatus.Installing
            }, cancellationToken).ConfigureAwait(false);

            states.Add(await _dependencyInstaller.EnsureAsync(requirement, cancellationToken).ConfigureAwait(false));
        }

        var alias = await ResolveAliasAsync(request, cancellationToken).ConfigureAwait(false);
        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "update-workspace",
            Title = "Workspace",
            Message = "Migrando configuracion sin tocar ejercicios, usuario, logs ni API keys.",
            Percent = 68,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);
        workspace = await _workspaceManager.PrepareAsync(workspace, alias, cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "update-vscode",
            Title = "VS Code",
            Message = "Actualizando extension local y settings del workspace.",
            Percent = 78,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);
        await _vsCodeManager.PrepareAsync(workspace, cancellationToken).ConfigureAwait(false);

        await RevalidateExercismAsync(request, workspace, progress, 86, "update-exercism", cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "update-smoke",
            Title = "Validacion F9",
            Message = "Revalidando F9 despues de actualizar.",
            Percent = 94,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);
        _gistImporterManager.ValidateGistCatalogs(workspace);
        await _telemetryCompatibilityManager.ValidateBuildFlowAsync(workspace, cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "update-complete",
            Title = "Actualizacion",
            Message = "Actualizacion completada conservando el trabajo del estudiante.",
            Percent = 100,
            Status = DependencyStatus.Ready
        }, cancellationToken).ConfigureAwait(false);
        return workspace;
    }

    private async Task<string> ReinstallAsync(
        SetupRequest request,
        IProgressSink progress,
        CancellationToken cancellationToken)
    {
        var workspace = await ResolveWorkspaceAsync(request, cancellationToken).ConfigureAwait(false);
        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "reinstall-manifest",
            Title = "Reinstalacion",
            Message = "Leyendo manifest y conservando datos del estudiante.",
            Percent = 10,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        await _uninstallManager.UninstallAsync(allowAggressiveCleanup: false, dryRun: false, cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "reinstall-tools",
            Title = "Reinstalacion",
            Message = "Reparando herramientas gestionadas, PATH y workspace.",
            Percent = 35,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        var requirements = DependencyDetector.Requirements.Where(x => x.Required).ToArray();
        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        for (var i = 0; i < requirements.Length; i++)
        {
            var requirement = requirements[i];
            var force = manifest.Dependencies.TryGetValue(requirement.Id, out var entry) && entry.InstalledByEstudio;
            await progress.ReportAsync(new ProgressEvent
            {
                StepId = $"reinstall-{requirement.Id}",
                Title = requirement.DisplayName,
                Message = force
                    ? "Reinstalando componente gestionado por el configurador."
                    : "Reparando si falta, esta roto o esta desactualizado.",
                Percent = 35 + (i * 34.0 / requirements.Length),
                Dependency = requirement.Id,
                Status = DependencyStatus.Installing
            }, cancellationToken).ConfigureAwait(false);
            await _dependencyInstaller.EnsureAsync(requirement, force, cancellationToken).ConfigureAwait(false);
        }

        await _repairManager.RepairAsync(workspace, await ResolveAliasAsync(request, cancellationToken).ConfigureAwait(false), request.SkipGitHubLogin, cancellationToken)
            .ConfigureAwait(false);

        await RevalidateExercismAsync(request, workspace, progress, 78, "reinstall-exercism", cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "reinstall-smoke",
            Title = "Validacion F9",
            Message = "Ejecutando smoke test final sin commits automaticos.",
            Percent = 92,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);
        await _telemetryCompatibilityManager.ValidateBuildFlowAsync(workspace, cancellationToken).ConfigureAwait(false);

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = "reinstall",
            Title = "Reinstalacion",
            Message = "Reinstalacion completada conservando ejercicios, logs y usuario.",
            Percent = 100,
            Status = DependencyStatus.Ready
        }, cancellationToken).ConfigureAwait(false);
        return workspace;
    }

    private async Task RevalidateExercismAsync(
        SetupRequest request,
        string workspace,
        IProgressSink progress,
        double percent,
        string stepId,
        CancellationToken cancellationToken)
    {
        if (request.SkipExercism)
        {
            return;
        }

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = stepId,
            Title = "Exercism",
            Message = "Revalidando CLI y token de Exercism.",
            Percent = percent,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.ExercismToken))
        {
            await _exercismManager.ConfigureTokenAsync(request.ExercismToken, workspace, cancellationToken).ConfigureAwait(false);
            return;
        }

        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (manifest.Exercism.Configured)
        {
            await _exercismManager.ValidateTokenAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await progress.ReportAsync(new ProgressEvent
        {
            StepId = stepId,
            Title = "Exercism",
            Message = "Falta token de Exercism para completar el flujo.",
            Percent = percent,
            Status = DependencyStatus.NeedsUserAction
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveWorkspaceAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        var alias = await ResolveAliasAsync(request, cancellationToken).ConfigureAwait(false);
        var target = request.WorkspacePath ?? _paths.GetRecommendedWorkspacePath(alias);
        return await _gitHubAccountManager.EnsureWorkspaceRepositoryAsync(target, alias, request.SkipGitHubLogin, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> ResolveKnownWorkspaceAsync(string? workspacePath, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            return workspacePath;
        }

        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        var alias = LocalAliasNormalizer.Normalize(manifest.LocalAlias, Environment.UserName);
        return manifest.WorkspacePath ?? _paths.GetRecommendedWorkspacePath(alias);
    }

    private async Task<string> ResolveAliasAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.LocalAlias))
        {
            return LocalAliasNormalizer.Normalize(request.LocalAlias);
        }

        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        return LocalAliasNormalizer.Normalize(manifest.LocalAlias, Environment.UserName);
    }

    private async Task<(UIStateSnapshot Snapshot, IReadOnlyList<DependencyState> Dependencies)> BuildCurrentStateAsync(
        string? workspacePath,
        string? localAlias,
        CancellationToken cancellationToken)
    {
        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        var alias = LocalAliasNormalizer.Normalize(localAlias, manifest.LocalAlias ?? Environment.UserName);
        var recommendedWorkspacePath = _paths.GetRecommendedWorkspacePath(alias);
        var workspace = workspacePath ?? manifest.WorkspacePath ?? recommendedWorkspacePath;
        var diagnostics = await _diagnosticsManager.RunAsync(workspace, cancellationToken).ConfigureAwait(false);
        var workspaceValid = !string.IsNullOrWhiteSpace(workspace) &&
                             Directory.Exists(workspace) &&
                             Directory.Exists(Path.Combine(workspace, ".git"));
        var buildFlowValid = manifest.BuildFlowValidated && workspaceValid;
        var smokeTestStatus = manifest.BuildFlowValidated
            ? "passed"
            : manifest.BuildFlowValidatedAtUtc is not null ? "failed" : "pending";
        var githubLogin = manifest.GitHub.UserName;
        var workspaceContext = new WorkspaceContextInfo
        {
            BaseRepo = ProductInfo.BaseRepository,
            WorkspaceRepo = string.IsNullOrWhiteSpace(githubLogin) ? null : $"{githubLogin}/{ProductInfo.RepositoryName}",
            LocalAlias = alias,
            GitHubLogin = githubLogin,
            WorkspacePath = workspace,
            RecommendedWorkspacePath = recommendedWorkspacePath
        };
        var vsCodeExtension = await _vsCodeManager.DiagnoseExtensionAsync(workspace, cancellationToken).ConfigureAwait(false);
        var extensionApiKeyConfig = _workspaceManager.DescribeExtensionApiKeyConfig(workspace);
        var finalReadiness = GlobalStateCalculator.CreateFinalReadinessCheck(
            diagnostics.Dependencies,
            manifest.GitHub,
            manifest.Exercism,
            workspaceValid,
            buildFlowValid,
            alias,
            workspace,
            smokeTestStatus);
        var resources = _planner.ToResourceStates(diagnostics.Dependencies, manifest.GitHub, manifest.Exercism, workspaceValid, workspace);

        return (new UIStateSnapshot
        {
            GlobalState = finalReadiness.GlobalState,
            GlobalMessage = finalReadiness.GlobalMessage,
            Resources = resources,
            GitHub = manifest.GitHub,
            Exercism = manifest.Exercism,
            WorkspacePath = workspace,
            RecommendedWorkspacePath = recommendedWorkspacePath,
            LocalAlias = alias,
            WorkspaceValid = workspaceValid,
            BuildFlowValid = buildFlowValid,
            WorkspaceContext = workspaceContext,
            VSCodeExtension = vsCodeExtension,
            ExtensionApiKeyConfig = extensionApiKeyConfig,
            FinalReadiness = finalReadiness
        }, diagnostics.Dependencies);
    }

    private async Task PersistBuildFlowValidationAsync(string? workspacePath, bool buildFlowValid, CancellationToken cancellationToken)
    {
        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _manifestManager.SaveAsync(manifest with
        {
            WorkspacePath = workspacePath ?? manifest.WorkspacePath,
            BuildFlowValidated = buildFlowValid,
            BuildFlowValidatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken).ConfigureAwait(false);
    }

    private Task LogFinalReadinessCheckAsync(UIStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            globalState = snapshot.GlobalState.ToString(),
            missingRequirements = snapshot.FinalReadiness.MissingRequirements,
            failedRequirements = snapshot.FinalReadiness.FailedRequirements,
            authRequirements = snapshot.FinalReadiness.AuthRequirements,
            smokeTestStatus = snapshot.FinalReadiness.SmokeTestStatus,
            alias = snapshot.FinalReadiness.Alias,
            githubLogin = snapshot.FinalReadiness.GitHubLogin,
            workspacePath = snapshot.FinalReadiness.WorkspacePath
        }, JsonDefaults.Options);
        return _logManager.WriteAsync("info", "final-readiness-check", payload, cancellationToken);
    }

    private static InstallerError NormalizeError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("WinGet", StringComparison.OrdinalIgnoreCase))
        {
            return InstallerError.FromException(ex, InstallerErrorCode.WINGET_INSTALL_FAILED);
        }

        if (message.Contains("Exercism", StringComparison.OrdinalIgnoreCase) &&
            (message.Contains("no autorizado", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("rechazado", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)))
        {
            return InstallerError.FromException(ex, InstallerErrorCode.EXERCISM_TOKEN_INVALID);
        }

        if (message.Contains("workspace", StringComparison.OrdinalIgnoreCase) && message.Contains("existe", StringComparison.OrdinalIgnoreCase))
        {
            return InstallerError.FromException(ex, InstallerErrorCode.WORKSPACE_LOCKED);
        }

        if (message.Contains("MSYS2", StringComparison.OrdinalIgnoreCase))
        {
            return InstallerError.FromException(ex, InstallerErrorCode.MSYS2_INSTALL_FAILED);
        }

        return InstallerError.FromException(ex);
    }
}
