using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Estudio.Setup.Core;
using Estudio.Setup.Windows;
using Forms = System.Windows.Forms;

namespace Estudio.Setup.WindowsHost;

public partial class MainWindow : Window
{
    private readonly WindowsSetupSession _session;
    private readonly DispatcherTimer _exercismValidationTimer;
    private bool _isPresentingError;

    public MainWindow()
    {
        InitializeComponent();

        var parsed = SetupModeParser.Parse(Environment.GetCommandLineArgs().Skip(1).ToArray());
        var baselineOptions = parsed with
        {
            Engine = SetupExecutionEngine.DesiredState,
            TuiRequested = false,
            JsonProgressRequested = false,
        };
        _session = new WindowsSetupSession(baselineOptions: baselineOptions);
        DataContext = _session;
        _exercismValidationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700),
        };
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoadedAsync;
        Activated += OnActivatedAsync;
        _session.PropertyChanged += OnSessionPropertyChanged;
        _exercismValidationTimer.Tick += OnExercismValidationTickAsync;

        PrimaryButton.Click += OnPrimaryButtonClickAsync;
        ChangeFolderButton.Click += OnChangeFolderClick;
        OpenGitHubButton.Click += OnOpenGitHubClickAsync;
        ChangeGitHubAccountButton.Click += OnChangeGitHubAccountClickAsync;
        RetryGitHubActionButton.Click += OnRetryGitHubActionClickAsync;
        OpenExercismButton.Click += (_, _) => _session.OpenExercismTokenPage();
        ExercismTokenTextBox.TextChanged += OnExercismTokenTextChanged;
        RetryButton.Click += OnRetryClickAsync;
        GuidedHelpButton.Click += OnGuidedHelpClick;
        OpenWorkspaceButton.Click += (_, _) => _session.OpenWorkspaceInVsCode();
        RefreshButtons();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowsTitleBarStyling.ApplyDarkMode(this);
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await InstallerUiExceptionSupport.TryRunAsync(
            () => _session.InitializeAsync(CancellationToken.None),
            exception => InstallerUiExceptionSupport.CreateInitialReviewFailure(exception),
            state =>
            {
                ShowInitialReviewError(state);
                return Task.CompletedTask;
            });
        RefreshButtons();
    }

    private async void OnActivatedAsync(object? sender, EventArgs e)
    {
        await InstallerUiExceptionSupport.TryRunAsync(
            () => _session.RefreshAccountsAsync(CancellationToken.None, allowExternalGuidance: false),
            exception => InstallerUiExceptionSupport.CreateInitialReviewFailure(exception),
            state =>
            {
                ShowInitialReviewError(state);
                return Task.CompletedTask;
            });
    }

    private async void OnPrimaryButtonClickAsync(object sender, RoutedEventArgs e)
    {
        PrimaryButton.IsEnabled = false;
        HideInitialReviewError();
        var succeeded = await InstallerUiExceptionSupport.TryRunAsync(
            async () =>
            {
                switch (_session.CurrentScreen)
                {
                    case WindowsInstallerScreen.Welcome:
                        _session.GoToReview();
                        break;
                    case WindowsInstallerScreen.QuickReview:
                        _session.GoToAccounts();
                        break;
                    case WindowsInstallerScreen.Accounts:
                        await _session.StartInstallationAsync(SetupMode.Install, CancellationToken.None);
                        break;
                    case WindowsInstallerScreen.Preparing:
                        break;
                    case WindowsInstallerScreen.Finished:
                        _session.OpenWorkspaceInVsCode();
                        break;
                }
            },
            exception => InstallerUiExceptionSupport.CreateInitialReviewFailure(exception),
            state =>
            {
                ShowInitialReviewError(state);
                return Task.CompletedTask;
            });

        if (succeeded)
        {
            RefreshButtons();
        }

        RefreshButtons();
        PrimaryButton.IsEnabled = !_session.IsRunning;
    }

    private async void OnOpenGitHubClickAsync(object sender, RoutedEventArgs e)
    {
        await InstallerUiExceptionSupport.TryRunAsync(
            async () =>
            {
                if (_session.IsGitHubConnected)
                {
                    _session.ConfirmGitHubAccount();
                }
                else
                {
                    await _session.ConnectGitHubAsync(CancellationToken.None);
                }

                RefreshButtons();
            },
            exception => InstallerUiExceptionSupport.CreateUnexpectedFailure(exception),
            state =>
            {
                ShowGlobalError(state);
                return Task.CompletedTask;
            });
    }

    private async void OnChangeGitHubAccountClickAsync(object sender, RoutedEventArgs e)
    {
        await InstallerUiExceptionSupport.TryRunAsync(
            async () =>
            {
                var switchContext = await _session.GetGitHubAccountSwitchContextAsync(CancellationToken.None);
                var continueChange = ConfirmationDialogWindow.Show(
                    this,
                    "Cambiar cuenta de GitHub",
                    switchContext.HasMultipleAccounts
                        ? "Abriré GitHub CLI para que elijas otra cuenta ya conectada. Cuando termines, volveré a comprobar la sesión automáticamente."
                        : "Abriré GitHub CLI para que cambies la cuenta actual o conectes una distinta. Cuando termines, volveré a comprobar la sesión automáticamente.",
                    "Continuar",
                    "Cancelar");
                if (!continueChange)
                {
                    return;
                }

                if (switchContext.CanSwitchAutomatically)
                {
                    await _session.SwitchGitHubAccountAutomaticallyAsync(CancellationToken.None);
                    RefreshButtons();
                    return;
                }

                if (switchContext.HasMultipleAccounts)
                {
                    _session.StartGitHubSwitchSelectionInTerminal();
                }
                else
                {
                    _session.StartGitHubReLoginInTerminal();
                }

                RefreshButtons();
            },
            exception => InstallerUiExceptionSupport.CreateUnexpectedFailure(exception),
            state =>
            {
                ShowGlobalError(state);
                return Task.CompletedTask;
            });
    }

    private async void OnRetryGitHubActionClickAsync(object sender, RoutedEventArgs e)
    {
        await InstallerUiExceptionSupport.TryRunAsync(
            () => _session.RetryGitHubActionAsync(CancellationToken.None),
            exception => InstallerUiExceptionSupport.CreateUnexpectedFailure(exception),
            state =>
            {
                ShowGlobalError(state);
                return Task.CompletedTask;
            });
        RefreshButtons();
    }

    private async void OnRetryClickAsync(object sender, RoutedEventArgs e)
    {
        await InstallerUiExceptionSupport.TryRunAsync(
            () => _session.RetryFailedAsync(CancellationToken.None),
            exception => InstallerUiExceptionSupport.CreateUnexpectedFailure(exception),
            state =>
            {
                ShowGlobalError(state);
                return Task.CompletedTask;
            });
        RefreshButtons();
    }

    private void OnExercismTokenTextChanged(object sender, TextChangedEventArgs e)
    {
        _exercismValidationTimer.Stop();
        _exercismValidationTimer.Start();
    }

    private async void OnExercismValidationTickAsync(object? sender, EventArgs e)
    {
        _exercismValidationTimer.Stop();
        await InstallerUiExceptionSupport.TryRunAsync(
            () => _session.UpdateExercismTokenAsync(ExercismTokenTextBox.Text, CancellationToken.None),
            exception => InstallerUiExceptionSupport.CreateUnexpectedFailure(exception),
            state =>
            {
                ShowGlobalError(state);
                return Task.CompletedTask;
            });
    }

    private void OnGuidedHelpClick(object sender, RoutedEventArgs e)
    {
        var solution = _session.GetGuidedSolution();
        if (solution is null)
        {
            _session.OpenGuidedHelp();
            return;
        }

        var dialog = new GuidedSolutionWindow(solution, _session.OpenExternalHelp)
        {
            Owner = this,
        };
        dialog.ShowDialog();
    }

    private void OnChangeFolderClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            InitialDirectory = _session.InstallationFolder,
            ShowNewFolderButton = true,
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            _session.UseInstallationFolder(dialog.SelectedPath);
        }
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WindowsSetupSession.CurrentScreen) or nameof(WindowsSetupSession.HasFailures) or nameof(WindowsSetupSession.CanOpenWorkspace) or nameof(WindowsSetupSession.IsRunning) or nameof(WindowsSetupSession.CanStartInstallation))
        {
            Dispatcher.Invoke(RefreshButtons);
        }
    }

    private void RefreshButtons()
    {
        PrimaryButton.IsEnabled = !_session.IsRunning && _session.CurrentScreen switch
        {
            WindowsInstallerScreen.Accounts => _session.CanStartInstallation,
            WindowsInstallerScreen.Preparing => false,
            _ => true,
        };
        ChangeFolderButton.Visibility = _session.IsPreparingScreen || _session.IsFinishedScreen ? Visibility.Collapsed : Visibility.Visible;
        RetryButton.Visibility = _session.IsPreparingScreen && _session.HasFailures ? Visibility.Visible : Visibility.Collapsed;
        GuidedHelpButton.Visibility = _session.IsPreparingScreen && _session.HasFailures ? Visibility.Visible : Visibility.Collapsed;
        OpenWorkspaceButton.Visibility = _session.IsFinishedScreen && _session.CanOpenWorkspace ? Visibility.Visible : Visibility.Collapsed;

        PrimaryButton.Content = _session.CurrentScreen switch
        {
            WindowsInstallerScreen.Welcome => "Comenzar",
            WindowsInstallerScreen.QuickReview => "Continuar",
            WindowsInstallerScreen.Accounts => _session.CanStartInstallation ? "Preparar mi computadora" : "Continúa cuando todo esté listo",
            WindowsInstallerScreen.Preparing => "Preparando...",
            WindowsInstallerScreen.Finished => "Abrir Estudio Socrático",
            _ => "Continuar",
        };
    }

    public void HandleUnhandledException(Exception exception)
    {
        var state = _session.CurrentScreen is WindowsInstallerScreen.Welcome or WindowsInstallerScreen.QuickReview
            ? InstallerUiExceptionSupport.CreateInitialReviewFailure(exception)
            : InstallerUiExceptionSupport.CreateUnexpectedFailure(exception);

        if (_session.CurrentScreen is WindowsInstallerScreen.Welcome or WindowsInstallerScreen.QuickReview)
        {
            ShowInitialReviewError(state);
            return;
        }

        ShowGlobalError(state);
    }

    private void ShowInitialReviewError(InstallerUiErrorState state)
    {
        if (_isPresentingError)
        {
            return;
        }

        _isPresentingError = true;
        try
        {
            _session.GoToWelcome();
            InitialErrorHeadlineText.Text = state.Headline;
            InitialErrorBodyText.Text = state.Body;
            InitialErrorDetailsText.Text = state.TechnicalDetails;
            InitialErrorDetailsExpander.IsExpanded = !state.DetailsHiddenByDefault;
            InitialErrorPanel.Visibility = Visibility.Visible;
            StatusHeadlineText.Text = "Vamos a preparar Estudio Socrático en tu computadora.";
            StatusBodyText.Text = "El instalador creará tu espacio de trabajo, conectará tus cuentas y dejará VS Code listo para estudiar sin pasos técnicos.";
            RefreshButtons();
        }
        finally
        {
            _isPresentingError = false;
        }
    }

    private void HideInitialReviewError()
    {
        InitialErrorPanel.Visibility = Visibility.Collapsed;
        InitialErrorHeadlineText.Text = string.Empty;
        InitialErrorBodyText.Text = string.Empty;
        InitialErrorDetailsText.Text = string.Empty;
        InitialErrorDetailsExpander.IsExpanded = false;
    }

    private void ShowGlobalError(InstallerUiErrorState state)
    {
        System.Windows.MessageBox.Show(
            $"{state.Headline}{Environment.NewLine}{Environment.NewLine}{state.Body}",
            "Estudio Socrático",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}