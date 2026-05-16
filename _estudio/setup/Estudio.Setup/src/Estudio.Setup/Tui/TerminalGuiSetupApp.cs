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
            Title = "Estudio Socratico Setup 2.0 (Esc para salir)",
        };

        var title = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = $"Alias: {studentAlias} | Modo inicial: {options.Mode}",
        };
        var aliasLabel = new Label
        {
            X = 0,
            Y = 3,
            Text = "Alias:",
        };
        var aliasField = new TextField
        {
            X = Pos.Right(aliasLabel) + 1,
            Y = 3,
            Width = 24,
            Height = 1,
            Text = studentAlias,
        };
        var applyAliasButton = new Button
        {
            X = Pos.Right(aliasField) + 1,
            Y = 3,
            Text = "Aplicar alias",
        };
        var changeGitHubButton = new Button
        {
            X = Pos.Right(applyAliasButton) + 1,
            Y = 3,
            Text = "Cambiar GitHub",
        };
        var progressBar = new ProgressBar
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
            ProgressBarFormat = ProgressBarFormat.SimplePlusPercentage,
            ProgressBarStyle = ProgressBarStyle.Continuous,
        };
        var progressLabel = new Label
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Text = "Progreso: 0/0",
        };
        var components = new ListView
        {
            X = 0,
            Y = 5,
            Width = Dim.Percent(48),
            Height = Dim.Fill(4),
        };
        components.SetSource(componentItems);
        var log = new TextView
        {
            X = Pos.Right(components) + 1,
            Y = 5,
            Width = Dim.Fill(),
            Height = Dim.Fill(4),
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
        };
        var artifactLabel = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Text = "Estado: esperando primera ejecucion...",
        };
        var verifyButton = new Button
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Text = "Verificar",
        };
        var repairButton = new Button
        {
            X = Pos.Right(verifyButton) + 1,
            Y = Pos.AnchorEnd(1),
            Text = "Reparar",
        };
        var retryButton = new Button
        {
            X = Pos.Right(repairButton) + 1,
            Y = Pos.AnchorEnd(1),
            Text = "Reintentar fallidos",
        };
        var exitButton = new Button
        {
            X = Pos.AnchorEnd(8),
            Y = Pos.AnchorEnd(1),
            Text = "Salir",
        };

        window.Add(title, progressBar, progressLabel, aliasLabel, aliasField, applyAliasButton, changeGitHubButton, components, log, artifactLabel, verifyButton, repairButton, retryButton, exitButton);

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
                    title.Text = $"Alias: {runAlias} | Modo: {runOptions.Mode}";
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
                            title.Text = $"Alias: {currentAlias} | Modo: {runOptions.Mode}";
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
