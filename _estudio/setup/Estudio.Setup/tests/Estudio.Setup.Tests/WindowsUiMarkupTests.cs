namespace Estudio.Setup.Tests;

public sealed class WindowsUiMarkupTests
{
    [Fact]
    public void Welcome_markup_does_not_repeat_main_title()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath());

        Assert.Contains("x:Name=\"StatusHeadlineText\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBlock Text=\"Vamos a preparar Estudio Socrático en tu computadora.\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Welcome_quick_review_uses_one_way_binding_for_status_glyph()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath());

        Assert.Contains("StatusGlyph, Mode=OneWay", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Welcome_markup_contains_hidden_technical_details_panel()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath());

        Assert.Contains("x:Name=\"InitialErrorPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"Collapsed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Ver detalles técnicos\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void App_palette_uses_official_vscode_2026_dark_resources()
    {
        var xaml = File.ReadAllText(AppXamlPath());

        Assert.Contains("#FF121314", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FF191A1B", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FF202122", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FF297AA0", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FF8C8C8C", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void App_styles_replace_default_black_focus_rectangles()
    {
        var xaml = File.ReadAllText(AppXamlPath());

        Assert.Contains("FocusVisualStyle\" Value=\"{x:Null}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FocusBorderBrush", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void App_styles_use_dark_vscode_scrollbars_instead_of_default_white_scrollbars()
    {
        var xaml = File.ReadAllText(AppXamlPath());

        Assert.Contains("#33838485", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#66838485", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#99838485", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TargetType=\"ScrollBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VsCodeVerticalScrollBarTemplate", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"White\"", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_button_handler_is_wrapped_in_safe_runner()
    {
        var codeBehind = File.ReadAllText(MainWindowCodeBehindPath());

        Assert.Contains("private async void OnPrimaryButtonClickAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("InstallerUiExceptionSupport.TryRunAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ShowInitialReviewError", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void App_registers_global_exception_handlers()
    {
        var appCodeBehind = File.ReadAllText(AppCodeBehindPath());

        Assert.Contains("DispatcherUnhandledException += OnDispatcherUnhandledException", appCodeBehind, StringComparison.Ordinal);
        Assert.Contains("AppDomain.CurrentDomain.UnhandledException += OnUnhandledException", appCodeBehind, StringComparison.Ordinal);
        Assert.Contains("TaskScheduler.UnobservedTaskException += OnUnobservedTaskException", appCodeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_window_uses_dynamic_navigation_logo_and_scrollable_cards()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath());

        Assert.Contains("ItemsSource=\"{Binding NavigationSteps}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Icon=\"Assets/estudio.png\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ChangeGitHubAccountButton", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding GitHubPrimaryActionText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding ExercismPrimaryActionText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowExercismTokenInput", xaml, StringComparison.Ordinal);
        Assert.Contains("<ScrollViewer Grid.Row=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("RetryGitHubActionButton", xaml, StringComparison.Ordinal);
        Assert.Contains("GitHubActionErrorHeadline", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Estudio Socrático\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedFolderMessage", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_layout_separates_header_steps_and_footer_rows_without_overlap_primitives()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath());

        Assert.Contains("<Grid Grid.Row=\"0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ScrollViewer Grid.Row=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Tu espacio de estudio quedará listo con GitHub, Exercism y F9 dentro de VS Code.\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Canvas", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClipToBounds=\"True\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_project_sets_icon_and_dark_title_bar_hook()
    {
        var codeBehind = File.ReadAllText(MainWindowCodeBehindPath());
        var project = File.ReadAllText(WindowsProjectPath());

        Assert.Contains("WindowsTitleBarStyling.ApplyDarkMode(this)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("<ApplicationIcon>Assets\\estudio.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("Link=\"Assets\\estudio.png\"", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Change_account_flow_uses_single_dialog_and_valid_cli_commands()
    {
        var codeBehind = File.ReadAllText(MainWindowCodeBehindPath());
        var sessionCode = File.ReadAllText(SessionStatePath());

        Assert.Contains("Cambiar cuenta de GitHub", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_session.SwitchGitHubAccountAutomaticallyAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_session.StartGitHubSwitchSelectionInTerminal()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_session.StartGitHubReLoginInTerminal()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_session.RetryGitHubActionAsync", codeBehind, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(codeBehind, "ConfirmationDialogWindow.Show("));
        Assert.DoesNotContain("--yes", sessionCode, StringComparison.Ordinal);
        Assert.Contains("gh auth switch --hostname github.com", sessionCode, StringComparison.Ordinal);
        Assert.Contains("gh auth login --web --hostname github.com --git-protocol https", sessionCode, StringComparison.Ordinal);
        Assert.Contains("gh auth logout --hostname github.com --user", sessionCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_dialog_uses_dark_title_bar_hook_and_consistent_button_sizing()
    {
        var dialogXaml = File.ReadAllText(ConfirmationDialogXamlPath());
        var dialogCodeBehind = File.ReadAllText(ConfirmationDialogCodeBehindPath());

        Assert.Contains("Icon=\"Assets/estudio.png\"", dialogXaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"170\"", dialogXaml, StringComparison.Ordinal);
        Assert.Contains("WindowsTitleBarStyling.ApplyDarkMode(this)", dialogCodeBehind, StringComparison.Ordinal);
    }

    private static string MainWindowXamlPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup.Windows", "MainWindow.xaml"));
    }

    private static string AppXamlPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup.Windows", "App.xaml"));
    }

    private static string MainWindowCodeBehindPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup.Windows", "MainWindow.xaml.cs"));
    }

    private static string AppCodeBehindPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup.Windows", "App.xaml.cs"));
    }

    private static string WindowsProjectPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup.Windows", "Estudio.Setup.Windows.csproj"));
    }

    private static string ConfirmationDialogXamlPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup.Windows", "ConfirmationDialogWindow.xaml"));
    }

    private static string ConfirmationDialogCodeBehindPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup.Windows", "ConfirmationDialogWindow.xaml.cs"));
    }

    private static string SessionStatePath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Estudio.Setup", "Windows", "WindowsSetupSession.cs"));
    }

    [Fact]
    public void Welcome_default_copy_comes_from_session_state()
    {
        var session = new Estudio.Setup.Windows.WindowsSetupSession(
            baselineOptions: new Estudio.Setup.Core.SetupOptions(Estudio.Setup.Core.SetupMode.Verify),
            currentDirectoryProvider: () => Path.Combine(Path.GetTempPath(), "estudio-ui-current"),
            commandRunnerFactory: () => new NoopCommandRunner(),
            userProfileProvider: () => Path.GetTempPath());

        Assert.Equal("Vamos a preparar Estudio Socrático en tu computadora.", session.StatusHeadline);
        Assert.Equal("El instalador creará tu espacio de trabajo, conectará tus cuentas y dejará VS Code listo para estudiar sin pasos técnicos.", session.StatusBody);

        session.GoToReview();
        Assert.Equal("Revisemos lo básico antes de empezar.", session.StatusHeadline);
        Assert.Equal("Estoy comprobando que tu computadora puede preparar Estudio Socrático sin pasos manuales.", session.StatusBody);

        session.GoToAccounts();
        Assert.Equal("Conectemos tus cuentas.", session.StatusHeadline);
        Assert.Equal("Usaremos GitHub para crear tu copia del proyecto y Exercism para preparar tus ejercicios de C.", session.StatusBody);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class NoopCommandRunner : Estudio.Setup.Services.ICommandRunner
    {
        public Task<Estudio.Setup.Services.CommandResult> RunAsync(string fileName, string arguments, Estudio.Setup.Services.CommandExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(Estudio.Setup.Services.CommandResult.Success(string.Empty));
        }
    }
}