export function normalizeAliasInput(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");
}

export function recommendedWorkspacePath(referencePath, alias) {
  const safeAlias = normalizeAliasInput(alias) || "estudiante";
  const fallback = `Estudio-Socratico-${safeAlias}`;
  if (!referencePath) {
    return fallback;
  }

  const normalized = String(referencePath).replace(/\//g, "\\");
  const lastSlash = normalized.lastIndexOf("\\");
  if (lastSlash < 0) {
    return fallback;
  }

  return `${normalized.slice(0, lastSlash)}\\Estudio-Socratico-${safeAlias}`;
}

export function resolveWorkspaceSelection({ currentPath, manual, alias, referencePath }) {
  if (manual && String(currentPath || "").trim()) {
    return currentPath;
  }

  return recommendedWorkspacePath(referencePath, alias);
}
