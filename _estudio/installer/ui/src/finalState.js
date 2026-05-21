function messageFromError(error) {
  return error?.description || error?.message || error?.title || String(error || "Error desconocido");
}

export function finalStateSource(summary, snapshot) {
  return summary?.currentState || snapshot || null;
}

export function finalReadiness(summary, snapshot) {
  return finalStateSource(summary, snapshot)?.finalReadiness || null;
}

export function finalIssues(workflow, summary, snapshot) {
  if (workflow === "uninstall") return [];

  const issues = [];
  if (!summary?.succeeded && Array.isArray(summary?.errors)) {
    for (const error of summary.errors) {
      const text = messageFromError(error);
      if (text) issues.push(`Error: ${text}`);
    }
  }

  const readiness = finalReadiness(summary, snapshot);
  if (!readiness) return issues;

  for (const item of readiness.missingRequirements || []) issues.push(`Falta: ${item}`);
  for (const item of readiness.failedRequirements || []) issues.push(`Reparar: ${item}`);
  for (const item of readiness.authRequirements || []) issues.push(`Conectar: ${item}`);
  if (readiness.smokeTestStatus && readiness.smokeTestStatus !== "passed") {
    issues.push(`Smoke test F9: ${readiness.smokeTestStatus === "failed" ? "fallo" : "pendiente"}`);
  }

  return Array.from(new Set(issues));
}

export function finalTitle(workflow, ready, summary, snapshot) {
  if (workflow === "uninstall") return ready ? "Limpieza completada" : "Limpieza incompleta";
  if (ready) return "Todo está listo para estudiar C";
  if (summary && !summary.succeeded) return "No pudimos completar la configuración";
  const globalState = finalStateSource(summary, snapshot)?.globalState;
  if (globalState === "needsAuthentication") return "Faltan cuentas por conectar";
  if (globalState === "needsRepair") return "Hay componentes por reparar";
  if (globalState === "needsSetup") return "Faltan herramientas por instalar";
  if (globalState === "needsUserAction") return "Falta una validación para terminar";
  return "Configuración incompleta";
}

export function finalText(workflow, ready, summary, snapshot) {
  if (workflow === "uninstall" && ready) return "Se quitaron los elementos gestionados y se conservó el trabajo del estudiante.";
  if (ready) return finalStateSource(summary, snapshot)?.globalMessage || summary?.globalMessage || "VS Code, workspace, toolchain, GitHub, Exercism y F9 fueron validados.";
  if (summary && !summary.succeeded && Array.isArray(summary.errors) && summary.errors.length > 0) {
    return messageFromError(summary.errors[0]);
  }
  return "Esto es lo que falta exactamente para marcar el entorno como listo.";
}
