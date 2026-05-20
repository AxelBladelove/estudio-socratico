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
                var snapshot = await _engine.DiagnoseAsync().ConfigureAwait(false);
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
                _planCts?.Cancel();
                _planCts = new CancellationTokenSource();
                var setupRequest = new SetupRequest { Mode = SetupMode.Install };
                var summary = await _engine.RunAsync(setupRequest, this, _planCts.Token).ConfigureAwait(false);
                await EmitEventAsync(new BridgeEvent
                {
                    Type = BridgeEventType.VerificationCompleted,
                    Payload = summary
                }).ConfigureAwait(false);
                return summary;

            case BridgeAction.CancelPlan:
                _planCts?.Cancel();
                return new { cancelled = true };

            case BridgeAction.RepairComponent:
                var repairRequest = new SetupRequest { Mode = SetupMode.Repair };
                return await _engine.RunAsync(repairRequest, this).ConfigureAwait(false);

            case BridgeAction.ConfigureGithub:
                return await _engine.SwitchGitHubAccountAsync().ConfigureAwait(false);

            case BridgeAction.ChangeGithubAccount:
                return await _engine.SwitchGitHubAccountAsync().ConfigureAwait(false);

            case BridgeAction.ConfigureExercism:
                var token = GetPayloadString(request, "token");
                var workspace = GetPayloadString(request, "workspace");
                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentException("Se requiere un token de Exercism.");
                return await _engine.ConfigureExercismAsync(token, workspace ?? "").ConfigureAwait(false);

            case BridgeAction.OpenExercismTokenPage:
                Process.Start(new ProcessStartInfo("https://exercism.org/settings/api_cli")
                    { UseShellExecute = true });
                return new { opened = true };

            case BridgeAction.OpenVSCode:
                var vsWorkspace = GetPayloadString(request, "workspace") ?? "";
                if (string.IsNullOrWhiteSpace(vsWorkspace))
                {
                    throw new InvalidOperationException("No hay un workspace configurado todavia para abrir en VS Code.");
                }

                await _engine.VSCode.OpenWorkspaceAsync(vsWorkspace, CancellationToken.None).ConfigureAwait(false);
                return new { opened = true, path = vsWorkspace };

            case BridgeAction.OpenLogs:
                var logPath = _engine.Logs.InstallerLogPath;
                var logsRoot = Path.GetDirectoryName(logPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                Directory.CreateDirectory(logsRoot);
                var targetPath = File.Exists(logPath) ? logPath : logsRoot;
                Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
                return new { opened = true, path = targetPath };

            case BridgeAction.ExportDiagnostics:
                var diagSnapshot = await _engine.DiagnoseAsync().ConfigureAwait(false);
                var diagPath = _engine.Logs.DiagnosticsPath;
                return new
                {
                    path = diagPath,
                    state = diagSnapshot.GlobalState.ToString(),
                    generated = File.Exists(diagPath)
                };

            case BridgeAction.RunSmokeTest:
                var smokeResult = await _engine.RunAsync(
                    new SetupRequest { Mode = SetupMode.Diagnostics }, this).ConfigureAwait(false);
                return smokeResult;

            default:
                throw new InvalidOperationException($"Accion no permitida: {request.Action}");
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

    private static string? GetPayloadString(BridgeRequest request, string key)
    {
        if (request.Payload.TryGetValue(key, out var value) && value is JsonElement element)
        {
            return element.GetString();
        }
        return value?.ToString();
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
