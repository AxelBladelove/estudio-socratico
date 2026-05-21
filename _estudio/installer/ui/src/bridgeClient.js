/**
 * WebView2 bridge client for Estudio Socrático Configurator.
 * Codex should wire the UI to the C# WebViewBridge through this module.
 */

const pending = new Map();

function newId() {
  return `ui_${Date.now()}_${Math.random().toString(16).slice(2)}`;
}

export function isBridgeAvailable() {
  return Boolean(window.chrome?.webview?.postMessage);
}

export function requestBackend(type, payload = {}) {
  const id = newId();
  const message = { id, type, payload };

  if (!isBridgeAvailable()) {
    return Promise.reject(new Error(`Bridge unavailable for action: ${type}`));
  }

  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    window.chrome.webview.postMessage(message);
  });
}

window.__bridgeResponse = function bridgeResponse(raw) {
  const response = typeof raw === "string" ? JSON.parse(raw) : raw;
  const handler = pending.get(response.id);
  if (!handler) return;

  pending.delete(response.id);

  if (response.ok) {
    handler.resolve(response.payload);
  } else {
    handler.reject(response.error || new Error("Backend error"));
  }
};

window.__bridgeEvent = function bridgeEvent(raw) {
  const event = typeof raw === "string" ? JSON.parse(raw) : raw;
  window.dispatchEvent(new CustomEvent("estudio-bridge-event", { detail: event }));
};

export const BackendAction = {
  DiagnoseEnvironment: "DiagnoseEnvironment",
  GetCurrentState: "GetCurrentState",
  ApplyPlan: "ApplyPlan",
  ApplyWorkflow: "ApplyWorkflow",
  CancelPlan: "CancelPlan",
  RepairComponent: "RepairComponent",
  ConfigureGithub: "ConfigureGithub",
  ChangeGithubAccount: "ChangeGithubAccount",
  ConfigureExercism: "ConfigureExercism",
  OpenExercismTokenPage: "OpenExercismTokenPage",
  OpenExercismCTrack: "OpenExercismCTrack",
  OpenVSCode: "OpenVSCode",
  OpenExercisePanel: "OpenExercisePanel",
  ReinstallVSCodeExtension: "ReinstallVSCodeExtension",
  OpenExtensionApiKeyConfig: "OpenExtensionApiKeyConfig",
  RevealExtensionApiKeyConfig: "RevealExtensionApiKeyConfig",
  RevealInExplorer: "RevealInExplorer",
  OpenLogs: "OpenLogs",
  ExportDiagnostics: "ExportDiagnostics",
  RunSmokeTest: "RunSmokeTest",
  PreviewUninstall: "PreviewUninstall",
  ReinstallManaged: "ReinstallManaged",
  UninstallManaged: "UninstallManaged",
};
