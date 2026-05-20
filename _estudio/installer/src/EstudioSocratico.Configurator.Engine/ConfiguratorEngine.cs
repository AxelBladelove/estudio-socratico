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
        _detector = new DependencyDetector(runner);
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
        _uninstallManager = new UninstallManager(_paths, _manifestManager, _logManager, security);
        _repairManager = new RepairManager(_dependencyInstaller, _workspaceManager, _vsCodeManager, _gitHubAccountManager, _logManager);
        _reinstallManager = new ReinstallManager(_repairManager, _uninstallManager);
        _telemetryCompatibilityManager = new TelemetryCompatibilityManager(runner, _paths, _logManager);
        _gistImporterManager = new GistImporterManager();
        _planner = new SetupPlanner();
    }

    public LogManager Logs => _logManager;
    public ManifestManager Manifest => _manifestManager;
    public VSCodeManager VSCode => _vsCodeManager;
    public SetupPlanner Planner => _planner;

    public async Task<IReadOnlyList<DependencyState>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await _logManager.StartRunAsync(cancellationToken).ConfigureAwait(false);
        return await _detector.DetectAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Full diagnosis: dependencies + auth + workspace + build flow.
    /// Returns a UIStateSnapshot for the React UI.
    /// </summary>
    public async Task<UIStateSnapshot> DiagnoseAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        await _logManager.StartRunAsync(cancellationToken).ConfigureAwait(false);
        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        var github = manifest.GitHub;
        var exercism = manifest.Exercism;
        var workspace = workspacePath ?? manifest.WorkspacePath ?? _paths.RepoRoot ?? _paths.DefaultWorkspacePath;
        var diagnostics = await _diagnosticsManager.RunAsync(workspace, cancellationToken).ConfigureAwait(false);
        var dependencies = diagnostics.Dependencies;
        var workspaceValid = Directory.Exists(workspace) && Directory.Exists(Path.Combine(workspace, ".git"));
        var buildFlowValid = false; // Will be verified during apply

        var globalState = GlobalStateCalculator.Calculate(dependencies, github, exercism, workspaceValid, buildFlowValid);
        var resources = _planner.ToResourceStates(dependencies, github, exercism);

        return new UIStateSnapshot
        {
            GlobalState = globalState,
            GlobalMessage = GlobalStateCalculator.GetHumanMessage(globalState),
            Resources = resources,
            GitHub = github,
            Exercism = exercism,
            WorkspacePath = workspace,
            WorkspaceValid = workspaceValid,
            BuildFlowValid = buildFlowValid
        };
    }

    /// <summary>
    /// Create a setup plan from current diagnosis without applying changes.
    /// </summary>
    public async Task<SetupPlan> CreatePlanAsync(CancellationToken cancellationToken = default)
    {
        var dependencies = await _detector.DetectAllAsync(cancellationToken).ConfigureAwait(false);
        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        var workspace = manifest.WorkspacePath ?? _paths.RepoRoot ?? _paths.DefaultWorkspacePath;
        var workspaceValid = Directory.Exists(workspace) && Directory.Exists(Path.Combine(workspace, ".git"));

        return _planner.CreatePlan(dependencies, manifest.GitHub, manifest.Exercism, workspaceValid, false);
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

        await _logManager.StartRunAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            switch (request.Mode)
            {
                case SetupMode.Diagnostics:
                    await progress.ReportAsync(new ProgressEvent { StepId = "diagnostics", Title = "Diagnostico", Message = "Analizando entorno.", Percent = 10 }, cancellationToken)
                        .ConfigureAwait(false);
                    var report = await _diagnosticsManager.RunAsync(workspace, cancellationToken).ConfigureAwait(false);
                    states.AddRange(report.Dependencies);
                    break;

                case SetupMode.Uninstall:
                    await progress.ReportAsync(new ProgressEvent { StepId = "uninstall", Title = "Desinstalacion", Message = "Leyendo manifest.", Percent = 10 }, cancellationToken)
                        .ConfigureAwait(false);
                    await _uninstallManager.UninstallAsync(request.AllowAggressiveCleanup, cancellationToken).ConfigureAwait(false);
                    break;

                case SetupMode.Reinstall:
                    workspace = await ResolveWorkspaceAsync(request, cancellationToken).ConfigureAwait(false);
                    await _reinstallManager.ReinstallAsync(workspace, ResolveAlias(request), request.SkipGitHubLogin, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SetupMode.Repair:
                    workspace = await ResolveWorkspaceAsync(request, cancellationToken).ConfigureAwait(false);
                    await _repairManager.RepairAsync(workspace, ResolveAlias(request), request.SkipGitHubLogin, cancellationToken)
                        .ConfigureAwait(false);
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

        var diagnostics = await _diagnosticsManager.RunAsync(workspace, cancellationToken).ConfigureAwait(false);
        if (states.Count == 0)
        {
            states.AddRange(diagnostics.Dependencies);
        }

        // Compute honest global state
        var manifest = await _manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        var workspaceValid = !string.IsNullOrEmpty(workspace) && Directory.Exists(workspace) && Directory.Exists(Path.Combine(workspace, ".git"));
        var globalState = errors.Count > 0
            ? GlobalState.Failed
            : GlobalStateCalculator.Calculate(states, manifest.GitHub, manifest.Exercism, workspaceValid, workspaceValid);

        return new SetupSummary
        {
            Mode = request.Mode,
            Succeeded = errors.Count == 0 && globalState == GlobalState.ReadyToStudy,
            GlobalState = globalState,
            GlobalMessage = GlobalStateCalculator.GetHumanMessage(globalState),
            Dependencies = states,
            Errors = errors,
            WorkspacePath = workspace,
            LogPath = _logManager.InstallerLogPath,
            DiagnosticsPath = _logManager.DiagnosticsPath
        };
    }

    public Task<AccountState> SwitchGitHubAccountAsync(CancellationToken cancellationToken = default)
    {
        return _gitHubAccountManager.EnsureLoginAsync(switchAccount: true, cancellationToken);
    }

    public Task<AccountState> ConfigureExercismAsync(string token, string workspacePath, CancellationToken cancellationToken = default)
    {
        return _exercismManager.ConfigureTokenAsync(token, workspacePath, cancellationToken);
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

        var alias = ResolveAlias(request);
        var target = request.WorkspacePath ?? _paths.RepoRoot ?? _paths.DefaultWorkspacePath;
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

    private async Task<string> ResolveWorkspaceAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        var alias = ResolveAlias(request);
        var target = request.WorkspacePath ?? _paths.RepoRoot ?? _paths.DefaultWorkspacePath;
        return await _gitHubAccountManager.EnsureWorkspaceRepositoryAsync(target, alias, request.SkipGitHubLogin, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolveAlias(SetupRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.LocalAlias))
        {
            return request.LocalAlias.Trim();
        }

        return Environment.UserName;
    }

    private static InstallerError NormalizeError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("WinGet", StringComparison.OrdinalIgnoreCase))
        {
            return InstallerError.FromException(ex, InstallerErrorCode.WINGET_INSTALL_FAILED);
        }

        if (message.Contains("Exercism", StringComparison.OrdinalIgnoreCase) && message.Contains("token", StringComparison.OrdinalIgnoreCase))
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
