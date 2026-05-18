using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Windows;

public sealed class WindowsSetupSession : INotifyPropertyChanged, IDesiredStateSetupProgressSink
{
    private readonly SetupApplicationHost _host;
    private readonly Func<string> _currentDirectoryProvider;
    private readonly Func<ICommandRunner> _commandRunnerFactory;
    private readonly IUriLauncher _uriLauncher;
    private readonly IVsCodeWorkspaceLauncher _vsCodeWorkspaceLauncher;
    private readonly Func<string?> _userProfileProvider;
    private readonly SetupOptions _baselineOptions;
    private readonly Func<SetupOptions, string, string, ICommandRunner, IDesiredStateSetupProgressSink, CancellationToken, Task<DesiredStateSetupRunArtifacts>> _runDesiredStateAsync;
    private readonly Func<string, string?> _interactiveCommandLauncher;

    private SetupLaunchContext? _context;
    private SetupExecutionArtifacts? _lastArtifacts;
    private WindowsInstallerScreen _currentScreen;
    private string _installationFolder;
    private string _selectedFolderMessage;
    private string _githubStatus;
    private string _exercismStatus;
    private string _exercismToken;
    private string _statusHeadline;
    private string _statusBody;
    private string? _gitHubUserName;
    private string _gitHubActionErrorHeadline;
    private string _gitHubActionErrorBody;
    private string _gitHubActionErrorTechnicalDetails;
    private bool _isRunning;
    private bool _showTechnicalDetails;
    private bool _isRefreshingAccounts;
    private bool _isGitHubConnected;
    private bool _gitHubAccountConfirmed;
    private bool _isExercismReady;
    private bool _needsExercismTrackActivation;
    private GitHubRetryAction _lastGitHubRetryAction;

    public WindowsSetupSession(
        SetupApplicationHost? host = null,
        SetupOptions? baselineOptions = null,
        Func<string>? currentDirectoryProvider = null,
        Func<string>? appBaseDirectoryProvider = null,
        Func<ICommandRunner>? commandRunnerFactory = null,
        Func<string, string?>? interactiveCommandLauncher = null,
        IUriLauncher? uriLauncher = null,
        IVsCodeWorkspaceLauncher? vsCodeWorkspaceLauncher = null,
        Func<string?>? userProfileProvider = null,
        Func<SetupOptions, string, string, ICommandRunner, IDesiredStateSetupProgressSink, CancellationToken, Task<DesiredStateSetupRunArtifacts>>? runDesiredStateAsync = null)
    {
        _host = host ?? new SetupApplicationHost(appBaseDirectoryProvider?.Invoke(), commandRunnerFactory);
        _baselineOptions = baselineOptions ?? new SetupOptions(SetupMode.Verify, Engine: SetupExecutionEngine.DesiredState);
        _currentDirectoryProvider = currentDirectoryProvider ?? (() => Directory.GetCurrentDirectory());
        _commandRunnerFactory = commandRunnerFactory ?? (() => new ProcessCommandRunner());
        _interactiveCommandLauncher = interactiveCommandLauncher ?? LaunchInteractiveCommand;
        _uriLauncher = uriLauncher ?? new ShellUriLauncher();
        _vsCodeWorkspaceLauncher = vsCodeWorkspaceLauncher ?? new VsCodeWorkspaceLauncher();
        _userProfileProvider = userProfileProvider ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        _runDesiredStateAsync = runDesiredStateAsync ?? ((options, workspaceRoot, studentAlias, commandRunner, progress, cancellationToken) => new DesiredStateSetupCoordinator().RunAndPersistAsync(options, workspaceRoot, studentAlias, commandRunner, progress, cancellationToken));
        _currentScreen = WindowsInstallerScreen.Welcome;
        _installationFolder = _baselineOptions.WorkspaceRoot ?? ResolveUiDefaultWorkspaceRoot(_userProfileProvider);
        _selectedFolderMessage = string.Empty;
        _githubStatus = "Cuando termines, vuelve aqui. Yo seguire automaticamente.";
        _exercismStatus = "Abre la pagina del token, pegalo aqui y yo seguire automaticamente.";
        _exercismToken = string.Empty;
        _statusHeadline = "Vamos a preparar Estudio Socrático en tu computadora.";
        _statusBody = "El instalador creará tu espacio de trabajo, conectará tus cuentas y dejará VS Code listo para estudiar sin pasos técnicos.";
        _gitHubUserName = null;
        _gitHubActionErrorHeadline = string.Empty;
        _gitHubActionErrorBody = string.Empty;
        _gitHubActionErrorTechnicalDetails = string.Empty;
        _isGitHubConnected = false;
        _gitHubAccountConfirmed = false;
        _isExercismReady = false;
        _needsExercismTrackActivation = false;
        _lastGitHubRetryAction = GitHubRetryAction.None;
        QuickChecks = new ObservableCollection<InstallerQuickCheck>();
        NavigationSteps = new ObservableCollection<InstallerNavigationStep>();
        ProgressBlocks = new ObservableCollection<InstallerProgressBlock>(WindowsSetupExperienceMapper.CreateBlocks());
        RefreshQuickChecks();
        RefreshNavigationSteps();
    }

    public ObservableCollection<InstallerQuickCheck> QuickChecks { get; }
    public ObservableCollection<InstallerNavigationStep> NavigationSteps { get; }
    public ObservableCollection<InstallerProgressBlock> ProgressBlocks { get; }

    public WindowsInstallerScreen CurrentScreen
    {
        get => _currentScreen;
        private set
        {
            if (_currentScreen == value)
            {
                return;
            }

            _currentScreen = value;
            Notify(nameof(CurrentScreen));
            Notify(nameof(IsWelcomeScreen));
            Notify(nameof(IsQuickReviewScreen));
            Notify(nameof(IsAccountsScreen));
            Notify(nameof(IsPreparingScreen));
            Notify(nameof(IsFinishedScreen));
            RefreshNavigationSteps();
        }
    }

    public string InstallationFolder
    {
        get => _installationFolder;
        set
        {
            if (string.Equals(_installationFolder, value, StringComparison.Ordinal))
            {
                return;
            }

            _installationFolder = value;
            Notify(nameof(InstallationFolder));
        }
    }

    public string SelectedFolderMessage
    {
        get => _selectedFolderMessage;
        private set
        {
            if (string.Equals(_selectedFolderMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedFolderMessage = value;
            Notify(nameof(SelectedFolderMessage));
        }
    }

    public string GitHubStatus
    {
        get => _githubStatus;
        private set
        {
            if (string.Equals(_githubStatus, value, StringComparison.Ordinal))
            {
                return;
            }

            _githubStatus = value;
            Notify(nameof(GitHubStatus));
        }
    }

    public string GitHubCardTitle => IsGitHubConnected ? "GitHub conectado" : "Conectar GitHub";

    public string GitHubCardSubtitle => IsGitHubConnected
        ? string.IsNullOrWhiteSpace(_gitHubUserName)
            ? "Tu sesión de GitHub ya está lista."
            : $"Estás usando la cuenta @{_gitHubUserName}."
        : "Necesito abrir GitHub para conectar tu cuenta.";

    public string GitHubPrimaryActionText => IsGitHubConnected
        ? _gitHubAccountConfirmed
            ? "Cuenta lista"
            : "Usar esta cuenta"
        : "Abrir GitHub";

    public bool IsGitHubPrimaryActionEnabled => !IsRunning && (!IsGitHubConnected || !_gitHubAccountConfirmed);
    public bool IsGitHubConnected => _isGitHubConnected;
    public bool ShowGitHubChangeAccount => IsGitHubConnected;
    public bool HasGitHubActionError => !string.IsNullOrWhiteSpace(_gitHubActionErrorHeadline);
    public string GitHubActionErrorHeadline => _gitHubActionErrorHeadline;
    public string GitHubActionErrorBody => _gitHubActionErrorBody;
    public string GitHubActionErrorTechnicalDetails => _gitHubActionErrorTechnicalDetails;

    public string ExercismStatus
    {
        get => _exercismStatus;
        private set
        {
            if (string.Equals(_exercismStatus, value, StringComparison.Ordinal))
            {
                return;
            }

            _exercismStatus = value;
            Notify(nameof(ExercismStatus));
        }
    }

    public string ExercismCardTitle => IsExercismReady
        ? "Exercism listo"
        : _needsExercismTrackActivation
            ? "Activar ejercicios de C"
            : "Conectar Exercism";

    public string ExercismCardSubtitle => IsExercismReady
        ? "Tu cuenta ya puede descargar y validar ejercicios de C."
        : _needsExercismTrackActivation
            ? "Solo falta activar una vez el track de C en Exercism."
            : "Usaremos Exercism para preparar y validar tus ejercicios de C.";

    public string ExercismPrimaryActionText => _needsExercismTrackActivation || IsExercismReady
        ? "Abrir Exercism"
        : "Abrir página del token";

    public bool ShowExercismTokenInput => !IsExercismReady && !_needsExercismTrackActivation;
    public bool IsExercismReady => _isExercismReady;

    public string ExercismToken
    {
        get => _exercismToken;
        set
        {
            if (string.Equals(_exercismToken, value, StringComparison.Ordinal))
            {
                return;
            }

            _exercismToken = value;
            Notify(nameof(ExercismToken));
        }
    }

    public string StatusHeadline
    {
        get => _statusHeadline;
        private set
        {
            if (string.Equals(_statusHeadline, value, StringComparison.Ordinal))
            {
                return;
            }

            _statusHeadline = value;
            Notify(nameof(StatusHeadline));
        }
    }

    public string StatusBody
    {
        get => _statusBody;
        private set
        {
            if (string.Equals(_statusBody, value, StringComparison.Ordinal))
            {
                return;
            }

            _statusBody = value;
            Notify(nameof(StatusBody));
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            Notify(nameof(IsRunning));
        }
    }

    public bool ShowTechnicalDetails
    {
        get => _showTechnicalDetails;
        set
        {
            if (_showTechnicalDetails == value)
            {
                return;
            }

            _showTechnicalDetails = value;
            Notify(nameof(ShowTechnicalDetails));
        }
    }

    public string TechnicalDetails => string.Join(Environment.NewLine + Environment.NewLine, ProgressBlocks.Where(block => !string.IsNullOrWhiteSpace(block.TechnicalMessage)).Select(block => $"{block.Title}{Environment.NewLine}{block.TechnicalMessage}"));
    public bool HasFailures => ProgressBlocks.Any(block => block.Status == SetupExecutionBlockStatus.Failed);
    public bool CanOpenWorkspace => _lastArtifacts?.Success == true && _context is not null;
    public bool CanStartInstallation => IsGitHubConnected && _gitHubAccountConfirmed && IsExercismReady && !IsRunning;
    public string AccountsReadinessMessage => CanStartInstallation
        ? "Todo está listo para preparar tu computadora."
        : !IsGitHubConnected
            ? IsExercismReady
                ? "Conecta GitHub para continuar."
                : "Confirma GitHub y Exercism para continuar."
            : !_gitHubAccountConfirmed
                ? "Confirma si quieres usar esta cuenta de GitHub."
                : _needsExercismTrackActivation
                    ? "Abre Exercism una vez para activar los ejercicios de C y vuelve aquí."
                    : "Deja Exercism listo para continuar.";
    public bool IsWelcomeScreen => CurrentScreen == WindowsInstallerScreen.Welcome;
    public bool IsQuickReviewScreen => CurrentScreen == WindowsInstallerScreen.QuickReview;
    public bool IsAccountsScreen => CurrentScreen == WindowsInstallerScreen.Accounts;
    public bool IsPreparingScreen => CurrentScreen == WindowsInstallerScreen.Preparing;
    public bool IsFinishedScreen => CurrentScreen == WindowsInstallerScreen.Finished;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        RefreshQuickChecks();
        return RefreshAccountsAsync(cancellationToken, allowExternalGuidance: false);
    }

    public void GoToReview()
    {
        CurrentScreen = WindowsInstallerScreen.QuickReview;
        StatusHeadline = "Revisemos lo básico antes de empezar.";
        StatusBody = "Estoy comprobando que tu computadora puede preparar Estudio Socrático sin pasos manuales.";
    }

    public void GoToWelcome()
    {
        CurrentScreen = WindowsInstallerScreen.Welcome;
        StatusHeadline = "Vamos a preparar Estudio Socrático en tu computadora.";
        StatusBody = "El instalador creará tu espacio de trabajo, conectará tus cuentas y dejará VS Code listo para estudiar sin pasos técnicos.";
    }

    public void GoToAccounts()
    {
        CurrentScreen = WindowsInstallerScreen.Accounts;
        StatusHeadline = "Conectemos tus cuentas.";
        StatusBody = "Usaremos GitHub para crear tu copia del proyecto y Exercism para preparar tus ejercicios de C.";
    }

    public async Task ConnectGitHubAsync(CancellationToken cancellationToken)
    {
        ClearGitHubActionError();
        _gitHubAccountConfirmed = false;
        NotifyGitHubState();
        StartGitHubLoginInTerminal();
        await Task.CompletedTask;
    }

    public async Task<GitHubAccountSwitchContext> GetGitHubAccountSwitchContextAsync(CancellationToken cancellationToken)
    {
        var commandRunner = _commandRunnerFactory();
        return await GetGitHubAccountSwitchContextAsync(commandRunner, cancellationToken);
    }

    public void ConfirmGitHubAccount()
    {
        if (!IsGitHubConnected)
        {
            return;
        }

        ClearGitHubActionError();
        _gitHubAccountConfirmed = true;
        GitHubStatus = string.IsNullOrWhiteSpace(_gitHubUserName)
            ? "Usaré tu sesión actual para crear tu copia del proyecto."
            : $"Usaré @{_gitHubUserName} para crear tu copia del proyecto.";
        NotifyGitHubState();
    }

    public async Task<bool> SwitchGitHubAccountAutomaticallyAsync(CancellationToken cancellationToken)
    {
        ClearGitHubActionError();
        _lastGitHubRetryAction = GitHubRetryAction.SwitchAutomatically;
        var commandRunner = _commandRunnerFactory();
        _gitHubAccountConfirmed = false;
        NotifyGitHubState();

        var result = await commandRunner.RunAsync("gh", "auth switch --hostname github.com", cancellationToken);
        if (!result.WasStarted)
        {
            SetGitHubActionError(
                "No pude cambiar la cuenta automáticamente.",
                "Puedes intentarlo de nuevo o abrir una terminal para elegir otra cuenta.",
                result.StandardError);
            return false;
        }

        if (!result.IsSuccess)
        {
            SetGitHubActionError(
                "No pude cambiar la cuenta automáticamente.",
                "Puedes intentarlo de nuevo o abrir una terminal para elegir otra cuenta.",
                FirstNonEmptyLine(result.StandardError, result.StandardOutput));
            return false;
        }

        await RefreshGitHubStatusAsync(cancellationToken);
        return true;
    }

    public bool StartGitHubLoginInTerminal()
    {
        return StartGitHubInteractiveFlow(
            "gh auth login --web --hostname github.com --git-protocol https",
            "Abrí una terminal para conectar tu cuenta de GitHub. Cuando termines, vuelve aquí y seguiré automáticamente.",
            GitHubRetryAction.LoginInTerminal);
    }

    public bool StartGitHubSwitchSelectionInTerminal()
    {
        return StartGitHubInteractiveFlow(
            "gh auth switch --hostname github.com",
            "Abrí una terminal para que elijas la cuenta de GitHub. Cuando termines, vuelve aquí y la revalidaré automáticamente.",
            GitHubRetryAction.SwitchInTerminal);
    }

    public bool StartGitHubReLoginInTerminal()
    {
        var logoutCommand = string.IsNullOrWhiteSpace(_gitHubUserName)
            ? "gh auth logout --hostname github.com"
            : $"gh auth logout --hostname github.com --user {_gitHubUserName}";
        return StartGitHubInteractiveFlow(
            $"{logoutCommand} && gh auth login --web --hostname github.com --git-protocol https",
            "Abrí una terminal para iniciar sesión con otra cuenta de GitHub. Cuando termines, vuelve aquí y la revalidaré automáticamente.",
            GitHubRetryAction.ReLoginInTerminal);
    }

    public async Task RetryGitHubActionAsync(CancellationToken cancellationToken)
    {
        switch (_lastGitHubRetryAction)
        {
            case GitHubRetryAction.SwitchAutomatically:
                await SwitchGitHubAccountAutomaticallyAsync(cancellationToken);
                break;
            case GitHubRetryAction.LoginInTerminal:
                StartGitHubLoginInTerminal();
                break;
            case GitHubRetryAction.SwitchInTerminal:
                StartGitHubSwitchSelectionInTerminal();
                break;
            case GitHubRetryAction.ReLoginInTerminal:
                StartGitHubReLoginInTerminal();
                break;
        }
    }

    public void OpenExercismTokenPage()
    {
        var target = _needsExercismTrackActivation || IsExercismReady
            ? Steps.ExercismCTrackStep.CTrackUrl
            : Steps.ExercismCTrackStep.TokenUrl;
        _uriLauncher.Open(target);
        ExercismStatus = _needsExercismTrackActivation
            ? "Activa los ejercicios de C en Exercism y vuelve aquí. Yo seguiré automáticamente."
            : "Cuando copies el token, pégalo aquí. Yo lo validaré automáticamente.";
    }

    public void UseInstallationFolder(string folder)
    {
        InstallationFolder = folder;
        SelectedFolderMessage = $"Tu carpeta de estudio será {folder}.";
        RefreshQuickChecks();
    }

    public async Task StartInstallationAsync(SetupMode mode, CancellationToken cancellationToken)
    {
        await RunInstallationAsync(BuildRunOptions(mode), cancellationToken);
    }

    public Task RetryFailedAsync(CancellationToken cancellationToken)
    {
        var failedBlockIds = ProgressBlocks.Where(block => block.Status == SetupExecutionBlockStatus.Failed).Select(block => block.Id).ToArray();
        if (failedBlockIds.Length == 0)
        {
            return Task.CompletedTask;
        }

        return RunInstallationAsync(BuildRunOptions(SetupMode.Repair, failedBlockIds), cancellationToken);
    }

    private async Task RunInstallationAsync(SetupOptions options, CancellationToken cancellationToken)
    {
        ResetBlocks();
        IsRunning = true;
        CurrentScreen = WindowsInstallerScreen.Preparing;
        StatusHeadline = "Preparando herramientas...";
        StatusBody = "Esto puede tomar algunos minutos. No necesitas abrir terminales ni scripts.";

        Environment.SetEnvironmentVariable(Steps.ExercismCTrackStep.TokenEnvironmentVariable, string.IsNullOrWhiteSpace(ExercismToken) ? null : ExercismToken);

        _context = _host.CreateLaunchContext(options, _currentDirectoryProvider());
        var artifacts = await _runDesiredStateAsync(
            _context.Options,
            _context.WorkspaceRoot,
            _context.StudentAlias,
            _context.CommandRunner,
            this,
            cancellationToken);
        _lastArtifacts = SetupExecutionArtifacts.FromDesiredState(artifacts);

        if (_lastArtifacts.Success)
        {
            StatusHeadline = "Todo esta listo.";
            StatusBody = "Puedes empezar con F9 dentro de VS Code.";
            CurrentScreen = WindowsInstallerScreen.Finished;
        }
        else
        {
            var failed = ProgressBlocks.FirstOrDefault(block => block.Status == SetupExecutionBlockStatus.Failed);
            if (failed is not null)
            {
                failed.HumanMessage = WindowsSetupExperienceMapper.HumanFailureMessage(failed);
                StatusHeadline = failed.HumanMessage;
                StatusBody = "Puedes reintentar este bloque o abrir una solucion guiada sin salir del instalador.";
            }
        }

        Notify(nameof(TechnicalDetails));
        Notify(nameof(HasFailures));
        Notify(nameof(CanOpenWorkspace));
        IsRunning = false;
    }

    public void OpenGuidedHelp()
    {
        var failed = ProgressBlocks.FirstOrDefault(block => block.Status == SetupExecutionBlockStatus.Failed);
        var target = failed is null ? null : WindowsSetupExperienceMapper.GuidedHelpUrl(failed);
        if (!string.IsNullOrWhiteSpace(target))
        {
            _uriLauncher.Open(target);
        }
    }

    public void OpenWorkspaceInVsCode()
    {
        if (_context is null)
        {
            return;
        }

        _vsCodeWorkspaceLauncher.OpenWorkspace(_context.WorkspaceRoot, _context.StudentAlias, string.IsNullOrWhiteSpace(ExercismToken) ? null : ExercismToken);
    }

    public GuidedSolution? GetGuidedSolution(string? blockId = null)
    {
        var selectedBlockId = blockId;
        if (string.IsNullOrWhiteSpace(selectedBlockId))
        {
            selectedBlockId = ProgressBlocks.FirstOrDefault(block => block.Status == SetupExecutionBlockStatus.Failed)?.Id;
        }

        return string.IsNullOrWhiteSpace(selectedBlockId) ? null : GuidedSolutionCatalog.ForBlock(selectedBlockId);
    }

    public void OpenExternalHelp(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        _uriLauncher.Open(url);
    }

    public async Task RefreshAccountsAsync(CancellationToken cancellationToken, bool allowExternalGuidance)
    {
        if (_isRefreshingAccounts || IsRunning)
        {
            return;
        }

        _isRefreshingAccounts = true;
        try
        {
            await RefreshGitHubStatusAsync(cancellationToken);
            await RefreshExercismStatusAsync(cancellationToken, allowExternalGuidance);
        }
        finally
        {
            _isRefreshingAccounts = false;
        }
    }

    public async Task UpdateExercismTokenAsync(string token, CancellationToken cancellationToken)
    {
        ExercismToken = token;
        if (string.IsNullOrWhiteSpace(token))
        {
            SetExercismState(isReady: false, needsTrackActivation: false);
            ExercismStatus = "Abre la página del token, pégalo aquí y yo seguiré automáticamente.";
            return;
        }

        ExercismStatus = "Recibí tu token. Voy a validarlo automáticamente.";
        await RefreshExercismStatusAsync(cancellationToken, allowExternalGuidance: true);
    }

    public Task ReportAsync(DesiredStateSetupProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        WindowsSetupExperienceMapper.ApplyProgress(ProgressBlocks, progressEvent);
        if (progressEvent is DesiredStateNodePhaseStarted started)
        {
            StatusHeadline = ProgressBlocks.First(block => block.Id == started.NodeId).Title;
            StatusBody = started.Phase == "verify"
                ? "Probando que todo funcione..."
                : started.HumanMessage;
        }

        Notify(nameof(TechnicalDetails));
        Notify(nameof(HasFailures));
        return Task.CompletedTask;
    }

    private IEnumerable<InstallerQuickCheck> BuildQuickChecks()
    {
        var userProfile = _userProfileProvider() ?? string.Empty;
        var isWindows = OperatingSystem.IsWindows();
        var hasInternet = HasInternetConnection();
        yield return new InstallerQuickCheck("Versión de Windows", isWindows, isWindows ? "Tu Windows es compatible con este instalador." : "Este instalador necesita Windows para continuar.");
        yield return new InstallerQuickCheck("Conexión a internet", hasInternet, hasInternet ? "Puedo hablar con GitHub y Exercism ahora mismo." : "Ahora mismo no logro conectar con GitHub. Revisa tu internet antes de continuar.");

        var writableFolder = EnsureWritableFolder(InstallationFolder, userProfile, out var writableMessage);
        yield return new InstallerQuickCheck("Permisos de escritura", writableFolder.IsReady, writableFolder.Message);
        yield return new InstallerQuickCheck("Carpeta de trabajo", writableFolder.IsReady, writableMessage);
    }

    private InstallerQuickCheck EnsureWritableFolder(string preferredFolder, string userProfile, out string folderMessage)
    {
        try
        {
            Directory.CreateDirectory(preferredFolder);
            var probePath = Path.Combine(preferredFolder, $".probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            folderMessage = $"Tu carpeta de estudio será {preferredFolder}.";
            SelectedFolderMessage = folderMessage;
            return new InstallerQuickCheck("Permisos de escritura", true, "Puedo crear tu espacio de estudio en la carpeta elegida.");
        }
        catch
        {
            var fallback = ResolveUiDefaultWorkspaceRoot(() => userProfile);
            Directory.CreateDirectory(fallback);
            InstallationFolder = fallback;
            folderMessage = $"No pude escribir en esa carpeta. Voy a usar users\\{Environment.UserName}\\Estudio Socratico.";
            SelectedFolderMessage = folderMessage;
            return new InstallerQuickCheck("Permisos de escritura", false, folderMessage);
        }
    }

    private static bool HasInternetConnection()
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3),
            };
            using var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com");
            using var response = client.Send(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task RefreshGitHubStatusAsync(CancellationToken cancellationToken)
    {
        var commandRunner = _commandRunnerFactory();
        var step = new Steps.GitHubAuthStep(commandRunner);
        var result = await step.VerifyAsync(new SetupContext(BuildRunOptions(SetupMode.Verify)), cancellationToken);
        if (!result.Success)
        {
            SetGitHubConnectionState(isConnected: false, userName: null);
            GitHubStatus = "Cuando termines, vuelve aquí. Yo seguiré automáticamente.";
            return;
        }

        var switchContext = await GetGitHubAccountSwitchContextAsync(commandRunner, cancellationToken);
        var profile = await commandRunner.RunAsync("gh", "api user", cancellationToken);
        var login = profile.IsSuccess ? TryParseGitHubLogin(profile.StandardOutput) : switchContext.ActiveUserName;
        SetGitHubConnectionState(isConnected: true, userName: login);
        ClearGitHubActionError();
        GitHubStatus = _gitHubAccountConfirmed
            ? string.IsNullOrWhiteSpace(_gitHubUserName)
                ? "Usaré tu sesión actual para crear tu copia del proyecto."
                : $"Usaré @{_gitHubUserName} para crear tu copia del proyecto."
            : "Confirma esta cuenta o cambia a otra si prefieres usar una sesión distinta.";
    }

    private async Task<GitHubAccountSwitchContext> GetGitHubAccountSwitchContextAsync(ICommandRunner commandRunner, CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync("gh", "auth status --json hosts", cancellationToken);
        if (!result.WasStarted || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return BuildFallbackGitHubSwitchContext();
        }

        return ParseGitHubAccountSwitchContext(result.StandardOutput, _gitHubUserName);
    }

    private async Task RefreshExercismStatusAsync(CancellationToken cancellationToken, bool allowExternalGuidance)
    {
        string? suggestedUrl = null;
        var commandRunner = _commandRunnerFactory();
        var step = new Steps.ExercismCTrackStep(
            commandRunner,
            tokenProvider: () => string.IsNullOrWhiteSpace(ExercismToken) ? null : ExercismToken.Trim(),
            openUrl: url => suggestedUrl = url);
        var context = new SetupContext(BuildRunOptions(string.IsNullOrWhiteSpace(ExercismToken) ? SetupMode.Verify : SetupMode.Install));
        var result = string.IsNullOrWhiteSpace(ExercismToken)
            ? await step.DetectAsync(context, cancellationToken)
            : await step.InstallAsync(context, cancellationToken);

        if (result.Success)
        {
            SetExercismState(isReady: true, needsTrackActivation: false);
            ExercismStatus = "Exercism conectado y tus ejercicios de C ya están listos.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(suggestedUrl) && suggestedUrl.Contains(Steps.ExercismCTrackStep.CTrackUrl, StringComparison.OrdinalIgnoreCase))
        {
            SetExercismState(isReady: false, needsTrackActivation: true);
            ExercismStatus = "Voy a abrir Exercism para que actives tus ejercicios de C una sola vez. Luego vuelve aquí.";
            if (allowExternalGuidance)
            {
                _uriLauncher.Open(suggestedUrl);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(ExercismToken))
        {
            SetExercismState(isReady: false, needsTrackActivation: false);
            ExercismStatus = "Recibí tu token. Voy a preparar Exercism automáticamente y te avisaré si falta un paso externo.";
            return;
        }

        SetExercismState(isReady: false, needsTrackActivation: false);
        ExercismStatus = "Abre la página del token, pégalo aquí y yo seguiré automáticamente.";
    }

    private void RefreshQuickChecks()
    {
        QuickChecks.Clear();
        foreach (var check in BuildQuickChecks())
        {
            QuickChecks.Add(check);
        }
    }

    private void RefreshNavigationSteps()
    {
        NavigationSteps.Clear();
        foreach (var step in BuildNavigationSteps())
        {
            NavigationSteps.Add(step);
        }
    }

    private IEnumerable<InstallerNavigationStep> BuildNavigationSteps()
    {
        var currentStep = CurrentScreen switch
        {
            WindowsInstallerScreen.Welcome => 1,
            WindowsInstallerScreen.QuickReview => 2,
            WindowsInstallerScreen.Accounts => 3,
            WindowsInstallerScreen.Preparing => 4,
            WindowsInstallerScreen.Finished => 5,
            _ => 1,
        };

        yield return CreateNavigationStep(1, "Bienvenida", "Elegimos tu carpeta de trabajo.", currentStep);
        yield return CreateNavigationStep(2, "Revisión rápida", "Comprobamos lo básico antes de empezar.", currentStep);
        yield return CreateNavigationStep(3, "Cuentas", "Conectamos GitHub y Exercism.", currentStep);
        yield return CreateNavigationStep(4, "Preparando herramientas", "Dejo tu entorno listo en VS Code.", currentStep);
        yield return CreateNavigationStep(5, "Final", "Abres tu espacio y empiezas a estudiar.", currentStep);
    }

    private static InstallerNavigationStep CreateNavigationStep(int number, string title, string description, int currentStep)
    {
        return new InstallerNavigationStep(number, title, description, isActive: currentStep == number, isCompleted: currentStep > number);
    }

    private void SetGitHubConnectionState(bool isConnected, string? userName)
    {
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();
        if (!isConnected)
        {
            normalizedUserName = null;
        }

        if (!string.Equals(_gitHubUserName, normalizedUserName, StringComparison.OrdinalIgnoreCase) || (!isConnected && _gitHubAccountConfirmed))
        {
            _gitHubAccountConfirmed = false;
        }

        _gitHubUserName = normalizedUserName;
        _isGitHubConnected = isConnected;
        NotifyGitHubState();
    }

    private bool StartGitHubInteractiveFlow(string command, string statusMessage, GitHubRetryAction retryAction)
    {
        ClearGitHubActionError();
        _lastGitHubRetryAction = retryAction;
        _gitHubAccountConfirmed = false;
        NotifyGitHubState();

        var technicalDetails = _interactiveCommandLauncher(BuildInteractiveCommand(command));
        if (string.IsNullOrWhiteSpace(technicalDetails))
        {
            GitHubStatus = statusMessage;
            return true;
        }

        SetGitHubActionError(
            "No pude abrir GitHub automáticamente.",
            "Puedes abrir GitHub manualmente o intentar de nuevo.",
            technicalDetails);
        return false;
    }

    private void SetGitHubActionError(string headline, string body, string? technicalDetails)
    {
        _gitHubActionErrorHeadline = headline;
        _gitHubActionErrorBody = body;
        _gitHubActionErrorTechnicalDetails = string.IsNullOrWhiteSpace(technicalDetails)
            ? "No se pudo iniciar el flujo de GitHub desde el instalador."
            : technicalDetails.Trim();
        NotifyGitHubErrorState();
    }

    private void ClearGitHubActionError()
    {
        if (string.IsNullOrWhiteSpace(_gitHubActionErrorHeadline)
            && string.IsNullOrWhiteSpace(_gitHubActionErrorBody)
            && string.IsNullOrWhiteSpace(_gitHubActionErrorTechnicalDetails))
        {
            return;
        }

        _gitHubActionErrorHeadline = string.Empty;
        _gitHubActionErrorBody = string.Empty;
        _gitHubActionErrorTechnicalDetails = string.Empty;
        NotifyGitHubErrorState();
    }

    private void NotifyGitHubErrorState()
    {
        Notify(nameof(HasGitHubActionError));
        Notify(nameof(GitHubActionErrorHeadline));
        Notify(nameof(GitHubActionErrorBody));
        Notify(nameof(GitHubActionErrorTechnicalDetails));
    }

    private void SetExercismState(bool isReady, bool needsTrackActivation)
    {
        _isExercismReady = isReady;
        _needsExercismTrackActivation = needsTrackActivation;
        NotifyExercismState();
    }

    private void NotifyGitHubState()
    {
        Notify(nameof(IsGitHubConnected));
        Notify(nameof(GitHubCardTitle));
        Notify(nameof(GitHubCardSubtitle));
        Notify(nameof(GitHubPrimaryActionText));
        Notify(nameof(IsGitHubPrimaryActionEnabled));
        Notify(nameof(ShowGitHubChangeAccount));
        Notify(nameof(CanStartInstallation));
        Notify(nameof(AccountsReadinessMessage));
    }

    private void NotifyExercismState()
    {
        Notify(nameof(IsExercismReady));
        Notify(nameof(ExercismCardTitle));
        Notify(nameof(ExercismCardSubtitle));
        Notify(nameof(ExercismPrimaryActionText));
        Notify(nameof(ShowExercismTokenInput));
        Notify(nameof(CanStartInstallation));
        Notify(nameof(AccountsReadinessMessage));
    }

    private static string? TryParseGitHubLogin(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("login", out var login) && login.ValueKind == JsonValueKind.String)
            {
                return login.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private GitHubAccountSwitchContext BuildFallbackGitHubSwitchContext()
    {
        if (string.IsNullOrWhiteSpace(_gitHubUserName))
        {
            return new GitHubAccountSwitchContext(null, Array.Empty<string>());
        }

        return new GitHubAccountSwitchContext(_gitHubUserName, new[] { _gitHubUserName });
    }

    private static GitHubAccountSwitchContext ParseGitHubAccountSwitchContext(string payload, string? fallbackUserName)
    {
        var knownUsers = new List<string>();
        var activeUserName = string.IsNullOrWhiteSpace(fallbackUserName) ? null : fallbackUserName.Trim();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("hosts", out var hosts)
                || !hosts.TryGetProperty("github.com", out var hostEntries)
                || hostEntries.ValueKind != JsonValueKind.Array)
            {
                return new GitHubAccountSwitchContext(activeUserName, activeUserName is null ? Array.Empty<string>() : new[] { activeUserName });
            }

            foreach (var entry in hostEntries.EnumerateArray())
            {
                if (!entry.TryGetProperty("login", out var loginElement) || loginElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var login = loginElement.GetString();
                if (string.IsNullOrWhiteSpace(login))
                {
                    continue;
                }

                if (!knownUsers.Contains(login, StringComparer.OrdinalIgnoreCase))
                {
                    knownUsers.Add(login);
                }

                if (entry.TryGetProperty("active", out var activeElement)
                    && activeElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && activeElement.GetBoolean())
                {
                    activeUserName = login;
                }
            }
        }
        catch (JsonException)
        {
        }

        if (knownUsers.Count == 0 && !string.IsNullOrWhiteSpace(activeUserName))
        {
            knownUsers.Add(activeUserName);
        }

        return new GitHubAccountSwitchContext(activeUserName, knownUsers);
    }

    private static string FirstNonEmptyLine(params string[] values)
    {
        foreach (var value in values)
        {
            using var reader = new StringReader(value ?? string.Empty);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }
        }

        return string.Empty;
    }

    private static string BuildInteractiveCommand(string command)
    {
        return $"title Estudio Socrático - GitHub && {command} && echo. && echo Vuelve al instalador cuando termines.";
    }

    private static string? LaunchInteractiveCommand(string command)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{command}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            });
            return null;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private void ResetBlocks()
    {
        ProgressBlocks.Clear();
        foreach (var block in WindowsSetupExperienceMapper.CreateBlocks())
        {
            ProgressBlocks.Add(block);
        }

        Notify(nameof(TechnicalDetails));
        Notify(nameof(HasFailures));
    }

    private void Notify(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string ResolveUiDefaultWorkspaceRoot(Func<string?> userProfileProvider)
    {
        var userProfile = userProfileProvider() ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = Environment.CurrentDirectory;
        }

        return Path.Combine(userProfile, "Estudio Socratico");
    }

    private SetupOptions BuildRunOptions(SetupMode mode, IReadOnlyList<string>? onlyNodeIds = null, bool forceGitHubRelogin = false)
    {
        return _baselineOptions with
        {
            Mode = mode,
            WorkspaceRoot = InstallationFolder,
            OnlyStepIds = onlyNodeIds,
            ForceGitHubRelogin = forceGitHubRelogin,
            TuiRequested = false,
            JsonProgressRequested = false,
            Engine = SetupExecutionEngine.DesiredState,
        };
    }

    private enum GitHubRetryAction
    {
        None,
        LoginInTerminal,
        SwitchAutomatically,
        SwitchInTerminal,
        ReLoginInTerminal,
    }
}