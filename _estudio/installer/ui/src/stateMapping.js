export const WORKFLOW_TO_MODE = {
  setup: "Install",
  update: "Update",
  repair: "Repair",
  reinstall: "Reinstall",
  accounts: "Diagnostics",
  uninstall: "Uninstall",
};

export const TOOL_ORDER = [
  { id: "nodejs", aliases: ["node", "node.js lts"], group: "Base", name: "Node.js", icon: "nodejs" },
  { id: "python", aliases: [], group: "Base", name: "Python", icon: "python" },
  { id: "git", aliases: [], group: "GitHub", name: "Git", icon: "git" },
  { id: "githubcli", aliases: ["github-cli", "gh"], group: "GitHub", name: "GitHub CLI", icon: "githubcli" },
  { id: "github", aliases: ["github-auth", "cuenta github"], group: "GitHub", name: "GitHub", icon: "github" },
  { id: "vscode", aliases: ["vs-code", "visual studio code"], group: "Editor", name: "VS Code", icon: "vscode" },
  { id: "msys2", aliases: [], group: "C", name: "MSYS2", icon: "msys2" },
  { id: "gcc", aliases: ["gcc-ucrt64"], group: "C", name: "GCC", icon: "gcc" },
  { id: "make", aliases: [], group: "C", name: "Make", icon: "make" },
  { id: "exercismcli", aliases: ["exercism", "exercism-cli"], group: "Exercism", name: "Exercism CLI", icon: "exercism" },
  { id: "exercism-token", aliases: ["token exercism", "exercism-auth"], group: "Exercism", name: "Token Exercism", icon: "exercism" },
  { id: "workspace", aliases: [], group: "Workspace", name: "Workspace", icon: "workspace" },
];

export function modeForWorkflow(workflow) {
  return WORKFLOW_TO_MODE[workflow] || "Install";
}

export function normalizeStatus(status) {
  const value = String(status || "unknown").toLowerCase();
  if (value === "ready" || value === "repaired") return "ready";
  if (value === "missing") return "missing";
  if (value === "broken" || value === "outdated" || value === "repair" || value === "repairing" || value === "partial") return "repair";
  if (value === "needsauth" || value === "needs-auth" || value === "needsuseraction" || value === "needs-user-action" || value === "action") return "action";
  if (value === "failed" || value === "error") return "error";
  if (value === "installing" || value === "enproceso") return "installing";
  if (value === "skipped") return "skipped";
  return "unknown";
}

export function statusLabel(status) {
  const normalized = normalizeStatus(status);
  if (normalized === "ready") return "Listo";
  if (normalized === "missing") return "Por instalar";
  if (normalized === "repair") return "Por reparar";
  if (normalized === "action") return "Requiere accion";
  if (normalized === "error") return "Error";
  if (normalized === "installing") return "En proceso";
  if (normalized === "skipped") return "Omitido";
  return "Revisando";
}

export function statusClass(status) {
  const normalized = normalizeStatus(status);
  if (normalized === "ready") return "status-ready";
  if (normalized === "missing") return "status-missing";
  if (normalized === "repair") return "status-repair";
  if (normalized === "action") return "status-action";
  if (normalized === "error") return "status-error";
  if (normalized === "installing") return "status-installing";
  return "";
}

export function toolsFromSnapshot(snapshot) {
  const resources = Array.isArray(snapshot?.resources) ? snapshot.resources : [];
  const byKey = new Map();

  for (const resource of resources) {
    const keys = [
      resource.id,
      resource.displayName,
      String(resource.id || "").replace(/[-_\s]/g, ""),
      String(resource.displayName || "").toLowerCase().replace(/[-_\s.]/g, ""),
    ].filter(Boolean);

    for (const key of keys) {
      byKey.set(String(key).toLowerCase(), resource);
    }
  }

  return TOOL_ORDER.map((tool) => {
    const keys = [
      tool.id,
      ...tool.aliases,
      tool.name,
      tool.name.toLowerCase().replace(/[-_\s.]/g, ""),
    ];
    const resource = keys.map((key) => byKey.get(String(key).toLowerCase())).find(Boolean);
    return {
      ...tool,
      status: normalizeStatus(resource?.status),
      label: statusLabel(resource?.status),
      detail: resource?.humanDescription || resource?.version || resource?.path || "",
      actionLabel: resource?.actionLabel || null,
      raw: resource || null,
    };
  });
}

export function summaryIsReady(workflow, summary, snapshot) {
  if (workflow === "uninstall") return Boolean(summary?.succeeded);
  if (summary) {
    if (!summary.succeeded) return false;
    const finalState = summary.currentState?.globalState || summary.globalState;
    return finalState === "readyToStudy";
  }
  return snapshot?.globalState === "readyToStudy";
}
