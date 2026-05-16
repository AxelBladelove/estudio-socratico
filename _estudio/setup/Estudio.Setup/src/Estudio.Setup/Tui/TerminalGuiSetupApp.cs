using System.Collections.ObjectModel;
using Estudio.Setup.Core;
using Estudio.Setup.Profile;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Estudio.Setup.Tui;

public static class TerminalGuiSetupApp
{
    public static Task<int> RunAsync(
        SetupOptions options,
        string workspaceRoot,
        string studentAlias,
        ICommandRunner commandRunner,
        CancellationToken cancellationToken)
    {
        using IApplication app = Application.Create();
        app.Init();

        var lastExitCode = 1;
        var isRunning = false;
        SetupRunArtifacts? lastArtifacts = null;
        var currentAlias = studentAlias;
        var baselineOptions = options with
        {
            AliasOverride = studentAlias,
            TuiRequested = true,
            ForceGitHubRelogin = false,
            OnlyStepIds = null,
        };
        var currentModel = CreateModel(options, commandRunner, studentAlias, workspaceRoot);
        var componentItems = new ObservableCollection<string>();
        var coordinator = new SetupRunCoordinator();

        using Window window = new()
        {
            Title = " Estudio Socratico Setup 2.0 ",
            SchemeName = SetupTuiTheme.Base,
        };

        var title = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(),
            Text = "Estudio Socratico Setup 2.0",
            SchemeName = SetupTuiTheme.Accent,
        };
        var contextLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Text = $"Alias: {studentAlias} | Modo inicial: {options.Mode} | Tema: EstudioDark",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var navigationLabel = new Label
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(2),
            Text = "Flechas: componentes | Tab: controles | Enter: ejecutar | Esc: salir",
            SchemeName = SetupTuiTheme.Menu,
        };
        var progressBar = new ProgressBar
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill(2),
            Height = 1,
            ProgressBarFormat = ProgressBarFormat.SimplePlusPercentage,
            ProgressBarStyle = ProgressBarStyle.Continuous,
            SchemeName = SetupTuiTheme.Accent,
        };
        var progressLabel = new Label
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(2),
            Text = "Progreso 0/0 | OK 0 | Avisos 0 | Fallos 0",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var accountFrame = new FrameView
        {
            X = 0,
            Y = 7,
            Width = Dim.Fill(),
            Height = 4,
            Title = " Cuenta ",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var aliasLabel = new Label
        {
            X = 1,
            Y = 0,
            Text = "Alias:",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var aliasField = new TextField
        {
            X = Pos.Right(aliasLabel) + 1,
            Y = 0,
            Width = 24,
            Height = 1,
            Text = studentAlias,
            SchemeName = SetupTuiTheme.Base,
        };
        var applyAliasButton = new Button
        {
            X = Pos.Right(aliasField) + 2,
            Y = 0,
            Text = "Aplicar alias",
            SchemeName = SetupTuiTheme.Accent,
        };
        var changeGitHubButton = new Button
        {
            X = Pos.Right(applyAliasButton) + 2,
            Y = 0,
            Text = "Cambiar GitHub",
            SchemeName = SetupTuiTheme.Accent,
        };
        var accountHelp = new Label
        {
            X = Pos.Right(changeGitHubButton) + 2,
            Y = 0,
            Width = Dim.Fill(1),
            Text = "local + GitHub CLI",
            SchemeName = SetupTuiTheme.Menu,
        };
        accountFrame.Add(aliasLabel, aliasField, applyAliasButton, changeGitHubButton, accountHelp);

        var componentsFrame = new FrameView
        {
            X = 0,
            Y = 11,
            Width = Dim.Percent(46),
            Height = Dim.Fill(5),
            Title = " Componentes ",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var components = new ListView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1),
            CanFocus = true,
            SchemeName = SetupTuiTheme.Base,
        };
        components.SetSource(componentItems);
        componentsFrame.Add(components);

        var activityFrame = new FrameView
        {
            X = Pos.Right(componentsFrame) + 1,
            Y = 11,
            Width = Dim.Fill(),
            Height = Dim.Fill(5),
            Title = " Actividad ",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var log = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            CanFocus = false,
            SchemeName = SetupTuiTheme.Base,
        };
        var logHelp = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Text = "Ultimos eventos del instalador",
            SchemeName = SetupTuiTheme.Menu,
        };
        activityFrame.Add(logHelp, log);

        var artifactLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(4),
            Width = Dim.Fill(2),
            Text = "Estado: esperando primera ejecucion...",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var verifyButton = new Button
        {
            X = 1,
            Y = Pos.AnchorEnd(2),
            Text = "Verificar",
            SchemeName = SetupTuiTheme.Accent,
        };
        var repairButton = new Button
        {
            X = Pos.Right(verifyButton) + 2,
            Y = Pos.AnchorEnd(2),
            Text = "Reparar",
            SchemeName = SetupTuiTheme.Accent,
        };
        var retryButton = new Button
        {
            X = Pos.Right(repairButton) + 2,
            Y = Pos.AnchorEnd(2),
            Text = "Reintentar fallidos",
            SchemeName = SetupTuiTheme.Accent,
        };
        var exitButton = new Button
        {
            X = Pos.AnchorEnd(9),
            Y = Pos.AnchorEnd(2),
            Text = "Salir",
            SchemeName = SetupTuiTheme.Dialog,
        };
        var footerHelp = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(2),
            Text = "Los avisos no bloquean si la herramienta queda verificada. Reintentar fallidos usa solo los componentes con MISS/FAIL.",
            SchemeName = SetupTuiTheme.Menu,
        };

        window.Add(title, contextLabel, navigationLabel, progressBar, progressLabel, accountFrame, componentsFrame, activityFrame, artifactLabel, verifyButton, repairButton, retryButton, exitButton, footerHelp);

        verifyButton.Accepted += (_, _) => StartRun(SetupTuiRunPlanner.ForMode(baselineOptions, SetupMode.Verify));
        repairButton.Accepted += (_, _) => StartRun(SetupTuiRunPlanner.ForMode(baselineOptions, SetupMode.Repair));
        applyAliasButton.Accepted += (_, _) =>
        {
            var requestedAlias = aliasField.Text?.ToString()?.Trim() ?? string.Empty;
            try
            {
                LocalStudentProfile.ValidateAlias(requestedAlias);
            }
            catch (ArgumentException ex)
            {
                artifactLabel.Text = $"Alias invalido: {ex.Message}";
                artifactLabel.SetNeedsDraw();
                return;
            }

            StartRun(SetupTuiRunPlanner.ChangeAlias(baselineOptions, requestedAlias));
        };
        changeGitHubButton.Accepted += (_, _) => StartRun(SetupTuiRunPlanner.ChangeGitHub(baselineOptions));
        retryButton.Accepted += (_, _) =>
        {
            var retryOptions = SetupTuiRunPlanner.RetryFailed(baselineOptions, currentModel.FailedStepIds());
            if (retryOptions is not null)
            {
                StartRun(retryOptions);
            }
        };
        exitButton.Accepted += (_, _) => app.RequestStop();

        Refresh(SetupTuiPresenter.CreateSnapshot(currentModel));
        components.SetFocus();
        _ = Task.Run(async () =>
        {
            await Task.Delay(150, cancellationToken);
            StartRun(options with { AliasOverride = studentAlias, TuiRequested = true });
        }, cancellationToken);

        app.Run(window);
        return Task.FromResult(lastExitCode);

        void StartRun(SetupOptions runOptions)
        {
            if (isRunning)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                isRunning = true;
                var runAlias = runOptions.AliasOverride ?? currentAlias;
                currentModel = CreateModel(runOptions, commandRunner, runAlias, workspaceRoot);
                var progress = new TerminalGuiProgressSink(currentModel, snapshot => app.Invoke(() => Refresh(snapshot)));

                app.Invoke(() =>
                {
                    SetButtonsEnabled(false);
                    contextLabel.Text = $"Alias: {runAlias} | Modo: {runOptions.Mode} | Tema: EstudioDark";
                    artifactLabel.Text = $"Ejecutando {runOptions.Mode}...";
                    Refresh(SetupTuiPresenter.CreateSnapshot(currentModel));
                });

                try
                {
                    lastArtifacts = await coordinator.RunAndPersistAsync(
                        runOptions,
                        workspaceRoot,
                        runAlias,
                        commandRunner,
                        progress,
                        cancellationToken);
                    lastExitCode = lastArtifacts.Report.Success ? 0 : 1;
                    app.Invoke(() =>
                    {
                        artifactLabel.Text = $"Estado: {lastArtifacts.StatePath} | Reporte: {lastArtifacts.ReportPath}";
                        if (lastArtifacts.Report.Success)
                        {
                            currentAlias = runAlias;
                            baselineOptions = baselineOptions with
                            {
                                AliasOverride = currentAlias,
                                ForceGitHubRelogin = false,
                                OnlyStepIds = null,
                            };
                            aliasField.Text = currentAlias;
                            contextLabel.Text = $"Alias: {currentAlias} | Modo: {runOptions.Mode} | Tema: EstudioDark";
                        }
                    });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    lastExitCode = 2;
                    app.Invoke(() => artifactLabel.Text = "Estado: ejecucion cancelada.");
                }
                catch (Exception ex)
                {
                    lastExitCode = 2;
                    app.Invoke(() => artifactLabel.Text = $"Estado: error inesperado. {ex.Message}");
                }
                finally
                {
                    isRunning = false;
                    app.Invoke(() =>
                    {
                        Refresh(SetupTuiPresenter.CreateSnapshot(currentModel));
                        SetButtonsEnabled(true);
                    });
                }
            }, cancellationToken);
        }

        void Refresh(SetupTuiSnapshot snapshot)
        {
            componentItems.Clear();
            foreach (var line in snapshot.ComponentLines)
            {
                componentItems.Add(line);
            }

            progressLabel.Text = snapshot.ProgressText;
            progressBar.Fraction = currentModel.TotalCount == 0 ? 0 : (float)currentModel.CompletedCount / currentModel.TotalCount;
            log.Text = string.Join(Environment.NewLine, snapshot.LogLines);
            log.MoveEnd();
            components.SetNeedsDraw();
            progressBar.SetNeedsDraw();
            progressLabel.SetNeedsDraw();
            log.SetNeedsDraw();
            artifactLabel.SetNeedsDraw();
        }

        void SetButtonsEnabled(bool enabled)
        {
            verifyButton.Enabled = enabled;
            repairButton.Enabled = enabled;
            retryButton.Enabled = enabled && currentModel.FailedStepIds().Count > 0;
            applyAliasButton.Enabled = enabled;
            changeGitHubButton.Enabled = enabled;
            exitButton.Enabled = true;
        }
    }

    private static class SetupTuiTheme
    {
        public const string Accent = "Accent";
        public const string Base = "Base";
        public const string Dialog = "Dialog";
        public const string Menu = "Menu";
    }

    private static SetupTuiProgressModel CreateModel(
        SetupOptions options,
        ICommandRunner commandRunner,
        string studentAlias,
        string workspaceRoot)
    {
        var steps = DefaultSetupSteps.Create(
            commandRunner,
            studentAlias: studentAlias,
            workspaceRoot: workspaceRoot);
        var stepIds = steps.Select(step => step.Id);
        if (options.OnlyStepIds is { Count: > 0 })
        {
            var selected = new HashSet<string>(options.OnlyStepIds, StringComparer.OrdinalIgnoreCase);
            stepIds = stepIds.Where(selected.Contains);
        }

        return new SetupTuiProgressModel(stepIds);
    }

    private sealed class TerminalGuiProgressSink : ISetupProgressSink
    {
        private readonly SetupTuiProgressModel _model;
        private readonly Action<SetupTuiSnapshot> _refresh;

        public TerminalGuiProgressSink(SetupTuiProgressModel model, Action<SetupTuiSnapshot> refresh)
        {
            _model = model;
            _refresh = refresh;
        }

        public async Task ReportAsync(SetupProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            await _model.ReportAsync(progressEvent, cancellationToken);
            _refresh(SetupTuiPresenter.CreateSnapshot(_model));
        }
    }
}
