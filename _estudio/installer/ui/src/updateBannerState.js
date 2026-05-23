export function getUpdateBannerView(updateInfo, currentVersion) {
  if (!updateInfo || updateInfo.status !== "available" || !updateInfo.latestVersion) {
    return { visible: false };
  }

  const current = currentVersion ? `v${String(currentVersion).replace(/^v/i, "")}` : "versión instalada";
  const latest = `v${String(updateInfo.latestVersion).replace(/^v/i, "")}`;

  return {
    visible: true,
    title: "Actualización disponible",
    body: `Hay una nueva versión de Estudio Socrático: ${latest}. Instálala para recibir las últimas correcciones.`,
    currentVersion: current,
    latestVersion: latest,
    buttonLabel: "Actualizar ahora",
  };
}
