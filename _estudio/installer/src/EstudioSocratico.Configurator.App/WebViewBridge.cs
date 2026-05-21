using System.Diagnostics;
using System.Text.Json;
using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Microsoft.UI.Dispatching;
using Microsoft.Web.WebView2.Core;

namespace EstudioSocratico.Configurator.App;

/// <summary>
/// Mediates typed JSON messages between the React UI (WebView2) and the C# backend.
/// All communication uses PostWebMessageAsJson / WebMessageReceived.
/// Only whitelisted actions are allowed — no arbitrary command execution.
/// </summary>
public sealed class WebViewBridge : IProgressSink
{
    private readonly ConfiguratorEngine _engine;
    private readonly DispatcherQueue _dispatcherQueue;
    private CoreWebView2? _webView;
    private CancellationTokenSource? _planCts;

    public WebViewBridge(ConfiguratorEngine engine)
    {
        _engine = engine;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("WebViewBridge debe crearse en el hilo UI.");
    }

    public void Attach(CoreWebView2 webView)
    {
        _webView = webView;
        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>
    /// Handle incoming messages from the React UI.
    /// </summary>
    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        _ = HandleWebMessageReceivedAsync(args);
    }

    private async Task HandleWebMessageReceivedAsync(CoreWebView2WebMessageReceivedEventArgs args)
    {
        BridgeRequest? request = null;

        try
        {
            request = BridgeProtocol.ParseRequest(args.WebMessageAsJson);
            await _engine.Logs.WriteAsync("info", "bridge", $"Mensaje recibido: {request.Action} ({request.Id}).")
                .ConfigureAwait(false);

            var result = await DispatchAsync(request).ConfigureAwait(false);
            var response = BridgeProtocol.CreateSuccessResponse(request, result);
            await SendResponseAsync(response).ConfigureAwait(false);
            await _engine.Logs.WriteAsync("info", "bridge", $"Respuesta enviada: {response.Type} ({response.Id}) ok={response.Ok}.")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = request is null
                ? new InstallerError
                {
                    Code = InstallerErrorCode.UNKNOWN_ERROR,
                    Title = "Mensaje invalido desde la UI",
                    Description = ex.Message,
                    ProbableCause = "El frontend envio un payload que no coincide con el contrato del bridge.",
                    RecommendedAction = "Reinicia el configurador. Si el problema persiste, exporta el diagnostico y revisa los logs.",
                    TechnicalDetails = ex.ToString(),
                    CanRetry = true
                }
                : InstallerError.FromException(ex);

            await _engine.Logs.WriteErrorAsync(error).ConfigureAwait(false);
            await _engine.Logs.WriteAsync("error", "bridge", $"Error procesando solicitud {(request?.Id ?? "sin-id")}: {error.Description}")
                .ConfigureAwait(false);

            if (request is not null)
            {
                var response = BridgeProtocol.CreateErrorResponse(request.Id, error, request.Action);
                await SendResponseAsync(response).ConfigureAwait(false);
                await _engine.Logs.WriteAsync("info", "bridge", $"Respuesta de error enviada: {response.Type} ({response.Id}).")
                    .ConfigureAwait(false);
            }

            await EmitEventAsync(new BridgeEvent
            {
                Type = BridgeEventType.Error,
                Payload = error
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Route a request to the appropriate engine method.
    /// </summary>
    private async Task<object?> DispatchAsync(BridgeRequest request)
    {
        switch (request.Action)
        {
            case BridgeAction.GetCurrentState:
            case BridgeAction.DiagnoseEnvironment:
                await _engine.Logs.WriteAsync("info", "bridge", $"Inicio de diagnostico ({request.Id}).")
                    .ConfigureAwait(false);
                await EmitEventAsync(new BridgeEvent { Type = BridgeEventType.DiagnosticStarted }).ConfigureAwait(false);
                var snapshot = await _engine.DiagnoseAsync(
                    BridgePayload.GetString(request, "workspacePath", "workspace"),
                    BridgePayload.GetString(request, "localAlias", "alias")).ConfigureAwait(false);
                await EmitEventAsync(new BridgeEvent
                {
                    Type = BridgeEventType.DiagnosticCompleted,
                    Payload = snapshot
                }).ConfigureAwait(false);
                await _engine.Logs.WriteAsync("info", "bridge", $"Fin de diagnostico ({request.Id}) => {snapshot.GlobalState}.")
                    .ConfigureAwait(false);
                return snapshot;

            case BridgeAction.CreateSetupPlan:
                var plan = await _engine.CreatePlanAsync().ConfigureAwait(false);
                await EmitEventAsync(new BridgeEvent
                {
                    Type = BridgeEventType.PlanCreated,
                    Payload = plan
                }).ConfigureAwait(false);
                return plan;

            case BridgeAction.ApplyPlan:
            case BridgeAction.ApplyWorkflow:
                return await RunWorkflowAsync(request, BridgePayload.GetSetupMode(request, SetupMode.Install)).ConfigureAwait(false);

            case BridgeAction.ReinstallManaged:
                return await RunWorkflowAsync(request, SetupMode.Reinstall).ConfigureAwait(false);

            case BridgeAction.PreviewUninstall:
                return await _engine.PreviewUninstallAsync(
                    BridgePayload.GetBool(request, "allowAggressiveCleanup", defaultValue: false),
                    CancellationToken.None).ConfigureAwait(false);

            case BridgeAction.UninstallManaged:
                return await RunWorkflowAsync(request, SetupMode.Uninstall).ConfigureAwait(false);

            case BridgeAction.CancelPlan:
                _planCts?.Cancel();
                return new { cancelled = true };

            case BridgeAction.RepairComponent:
                return await RunWorkflowAsync(request, SetupMode.Repair).ConfigureAwait(false);

            case BridgeAction.ConfigureGithub:
                return await _engine.ConfigureGitHubAsync(
                    switchAccount: false,
                    workspacePath: BridgePayload.GetString(request, "workspacePath", "workspace")).ConfigureAwait(false);

            case BridgeAction.ChangeGithubAccount:
                return await _engine.ConfigureGitHubAsync(
                    switchAccount: true,
                    workspacePath: BridgePayload.GetString(request, "workspacePath", "workspace")).ConfigureAwait(false);

            case BridgeAction.ConfigureExercism:
                var token = BridgePayload.GetString(request, "token", "exercismToken");
                var workspace = BridgePayload.GetString(request, "workspacePath", "workspace");
                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentException("Se requiere un token de Exercism.");
                return await _engine.ConfigureExercismAsync(token, workspace).ConfigureAwait(false);

            case BridgeAction.OpenExercismTokenPage:
                Process.Start(new ProcessStartInfo(ExercismManager.TokenUrl)
                    { UseShellExecute = true });
                return new { opened = true, url = ExercismManager.TokenUrl };

            case BridgeAction.OpenExercismCTrack:
                Process.Start(new ProcessStartInfo(ExercismManager.CTrackUrl)
                    { UseShellExecute = true });
                return new { opened = true, url = ExercismManager.CTrackUrl };

            case BridgeAction.OpenVSCode:
                var vsWorkspace = BridgePayload.GetString(request, "workspacePath", "workspace");
                vsWorkspace = await _engine.GetKnownWorkspacePathAsync(vsWorkspace).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(vsWorkspace))
                {
                    throw new InvalidOperationException("No hay un workspace configurado todavia para abrir en VS Code.");
                }

                await _engine.VSCode.OpenWorkspaceAsync(vsWorkspace, CancellationToken.None).ConfigureAwait(false);
                return new { opened = true, path = vsWorkspace };

            case BridgeAction.OpenExercisePanel:
                await _engine.OpenExercisePanelAsync(
                    BridgePayload.GetString(request, "workspacePath", "workspace"),
                    CancellationToken.None).ConfigureAwait(false);
                return new { opened = true };

            case BridgeAction.ReinstallVSCodeExtension:
                await _engine.ReinstallVSCodeExtensionAsync(
                    BridgePayload.GetString(request, "workspacePath", "workspace"),
                    CancellationToken.None).ConfigureAwait(false);
                return new { repaired = true };

            case BridgeAction.OpenExtensionApiKeyConfig:
                var extensionConfig = await _engine.EnsureExtensionApiKeyConfigAsync(
                    BridgePayload.GetString(request, "workspacePath", "workspace"),
                    CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(extensionConfig.LocalConfigPath))
                {
                    throw new InvalidOperationException("No se pudo resolver la configuracion local de API Key.");
                }

                Process.Start(new ProcessStartInfo(extensionConfig.LocalConfigPath) { UseShellExecute = true });
                return new { opened = true, path = extensionConfig.LocalConfigPath };

            case BridgeAction.RevealExtensionApiKeyConfig:
                var revealConfig = await _engine.GetExtensionApiKeyConfigAsync(
                    BridgePayload.GetString(request, "workspacePath", "workspace"),
                    CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(revealConfig.LocalConfigPath))
                {
                    throw new InvalidOperationException("No se pudo resolver la configuracion local de API Key.");
                }

                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{revealConfig.LocalConfigPath}\"")
                {
                    UseShellExecute = true
                });
                return new { opened = true, path = revealConfig.LocalConfigPath };

            case BridgeAction.RevealInExplorer:
                var revealPath = BridgePayload.GetString(request, "path");
                if (string.IsNullOrWhiteSpace(revealPath))
                {
                    throw new InvalidOperationException("No se recibio una ruta para revelar en el Explorador.");
                }

                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{revealPath}\"")
                {
                    UseShellExecute = true
                });
                return new { opened = true, path = revealPath };

            case BridgeAction.OpenLogs:
                var logPath = _engine.Logs.InstallerLogPath;
                var logsRoot = Path.GetDirectoryName(logPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                Directory.CreateDirectory(logsRoot);
                var targetPath = File.Exists(logPath) ? logPath : logsRoot;
                Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
                return new { opened = true, path = targetPath };

            case BridgeAction.ExportDiagnostics:
                var diagSnapshot = await _engine.DiagnoseAsync(BridgePayload.GetString(request, "workspacePath", "workspace")).ConfigureAwait(false);
                var diagPath = _engine.Logs.DiagnosticsPath;
                return new
                {
                    path = diagPath,
                    state = diagSnapshot.GlobalState.ToString(),
                    generated = File.Exists(diagPath)
                };

            case BridgeAction.RunSmokeTest:
                _planCts?.Cancel();
                _planCts = new CancellationTokenSource();
                await EmitEventAsync(new BridgeEvent { Type = BridgeEventType.VerificationStarted }).ConfigureAwait(false);
                var smokeSummary = await _engine.RunSmokeTestAsync(
                    BridgePayload.GetString(request, "workspacePath", "workspace"),
                    this,
                    _planCts.Token).ConfigureAwait(false);
                await EmitEventAsync(new BridgeEvent
                {
                    Type = BridgeEventType.VerificationCompleted,
                    Payload = smokeSummary
                }).ConfigureAwait(false);
                await EmitEventAsync(new BridgeEvent
                {
                    Type = BridgeEventType.GlobalStateChanged,
                    Payload = smokeSummary
                }).ConfigureAwait(false);
                return smokeSummary;

            default:
                throw new InvalidOperationException($"Accion no permitida: {request.Action}");
        }
    }

    private async Task<SetupSummary> RunWorkflowAsync(BridgeRequest request, SetupMode mode)
    {
        _planCts?.Cancel();
        _planCts = new CancellationTokenSource();

        var setupRequest = BridgePayload.ToSetupRequest(request, mode) with { Mode = mode };
        await EmitEventAsync(new BridgeEvent { Type = BridgeEventType.VerificationStarted }).ConfigureAwait(false);
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(_planCts.Token);
        var heartbeat = EmitHeartbeatAsync(heartbeatCts.Token);
        SetupSummary summary;
        try
        {
            summary = await _engine.RunAsync(setupRequest, this, _planCts.Token).ConfigureAwait(false);
        }
        finally
        {
            await heartbeatCts.CancelAsync().ConfigureAwait(false);
            await heartbeat.ConfigureAwait(false);
        }

        await EmitEventAsync(new BridgeEvent
        {
            Type = BridgeEventType.VerificationCompleted,
            Payload = summary
        }).ConfigureAwait(false);
        await EmitEventAsync(new BridgeEvent
        {
            Type = BridgeEventType.GlobalStateChanged,
            Payload = summary
        }).ConfigureAwait(false);
        return summary;
    }

    private async Task EmitHeartbeatAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
                var elapsed = DateTimeOffset.UtcNow - started;
                await EmitEventAsync(new BridgeEvent
                {
                    Type = BridgeEventType.StepProgress,
                    Payload = new ProgressEvent
                    {
                        StepId = "heartbeat",
                        Title = "Trabajo en curso",
                        Message = $"Operacion activa. Tiempo transcurrido: {elapsed:mm\\:ss}.",
                        Status = DependencyStatus.Installing
                    }
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// IProgressSink implementation — forwards engine progress events to the UI.
    /// </summary>
    public async Task ReportAsync(ProgressEvent progress, CancellationToken cancellationToken = default)
    {
        var eventType = progress.Status switch
        {
            DependencyStatus.Ready => BridgeEventType.StepSucceeded,
            DependencyStatus.Failed => BridgeEventType.StepFailed,
            DependencyStatus.Skipped => BridgeEventType.StepSkipped,
            DependencyStatus.Installing => BridgeEventType.StepStarted,
            DependencyStatus.NeedsAuth or DependencyStatus.NeedsUserAction => BridgeEventType.StepNeedsUserInput,
            _ => BridgeEventType.StepProgress
        };

        await EmitEventAsync(new BridgeEvent
        {
            Type = eventType,
            Payload = progress
        });
    }

    /// <summary>
    /// Send a response to a specific request from the UI.
    /// </summary>
    private async Task SendResponseAsync(BridgeResponse response)
    {
        if (_webView is null) return;
        await InvokeOnUiThreadAsync(async () =>
        {
            var json = JsonSerializer.Serialize(response, JsonDefaults.Options);
            var escaped = JsonSerializer.Serialize(json); // double-serialize for JS string
            await _webView.ExecuteScriptAsync(
                $"window.__bridgeResponse && window.__bridgeResponse({escaped})");
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Push an event to the UI without a corresponding request.
    /// </summary>
    public async Task EmitEventAsync(BridgeEvent evt)
    {
        if (_webView is null) return;
        await InvokeOnUiThreadAsync(async () =>
        {
            var json = JsonSerializer.Serialize(evt, JsonDefaults.Options);
            var escaped = JsonSerializer.Serialize(json);
            await _webView.ExecuteScriptAsync(
                $"window.__bridgeEvent && window.__bridgeEvent({escaped})");
        }).ConfigureAwait(false);
    }

    private Task InvokeOnUiThreadAsync(Func<Task> action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action().ConfigureAwait(true);
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("No se pudo reenviar la accion del bridge al hilo UI."));
        }

        return tcs.Task;
    }
}
