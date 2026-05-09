const vscode = require("vscode");
const cp = require("child_process");
const fs = require("fs");
const os = require("os");
const path = require("path");

let currentPanel;
let currentProvider;

function activate(context) {
  currentProvider = new ExerciseViewProvider(context);
  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider("estudioExercism.view", currentProvider),
    vscode.commands.registerCommand("estudioExercism.openPanel", () => openPanel(context)),
    vscode.commands.registerCommand("estudioExercism.testCurrent", () => runForCurrentFile("test")),
    vscode.commands.registerCommand("estudioExercism.submitCurrent", () => runForCurrentFile("submit")),
  );
}

function deactivate() {}

function getWorkspaceRoot() {
  const folder = vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders[0];
  if (!folder) {
    throw new Error("Abre primero la carpeta del repo Estudio Socratico en VS Code.");
  }
  return folder.uri.fsPath;
}

function getManagerPath(root) {
  return path.join(root, "soporte", "exercism", "manager.ps1");
}

function runManager(root, args, options = {}) {
  return new Promise((resolve, reject) => {
    const manager = getManagerPath(root);
    let outFile;
    const commandArgs = ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", manager, "-RepoRoot", root, ...args];
    if (options.jsonFile) {
      outFile = path.join(os.tmpdir(), `estudio-exercism-${Date.now()}-${Math.random().toString(16).slice(2)}.json`);
      commandArgs.push("-OutFile", outFile);
    }

    cp.execFile("powershell.exe", commandArgs, { cwd: root, maxBuffer: 1024 * 1024 * 20 }, (error, stdout, stderr) => {
      if (outFile && fs.existsSync(outFile)) {
        try {
          const text = fs.readFileSync(outFile, "utf8");
          fs.unlinkSync(outFile);
          resolve(text.trim());
          return;
        } catch (readError) {
          reject(readError);
          return;
        }
      }

      if (error) {
        if (stdout && stdout.trim()) {
          resolve(stdout.trim());
          return;
        }
        const message = stdout || stderr || error.message;
        reject(new Error(message.trim()));
        return;
      }
      resolve(stdout.trim());
    });
  });
}

function runManagerJson(root, args) {
  return runManager(root, args, { jsonFile: true }).then(parseJson);
}

function parseJson(text) {
  const cleaned = String(text || "").replace(/^\uFEFF/, "").trim();
  const jsonText = extractJson(cleaned);
  try {
    return JSON.parse(jsonText);
  } catch (error) {
    throw new Error(`El backend no devolvio JSON valido: ${cleaned.slice(0, 1200)}`);
  }
}

function extractJson(text) {
  if (!text) {
    return text;
  }

  const starts = ["{", "["]
    .map((char) => ({ char, index: text.indexOf(char) }))
    .filter((item) => item.index >= 0)
    .sort((a, b) => a.index - b.index);

  if (starts.length === 0) {
    return text;
  }

  const start = starts[0].index;
  const open = text[start];
  const close = open === "{" ? "}" : "]";
  const end = text.lastIndexOf(close);
  if (end <= start) {
    return text.slice(start);
  }

  return text.slice(start, end + 1);
}

class ExerciseViewProvider {
  constructor(context) {
    this.context = context;
    this.view = undefined;
  }

  resolveWebviewView(webviewView) {
    this.view = webviewView;
    webviewView.webview.options = { enableScripts: true };
    webviewView.webview.onDidReceiveMessage(async (message) => {
      await handleWebviewMessage(getWorkspaceRoot(), message, webviewView.webview);
    }, undefined, this.context.subscriptions);
    this.refresh();
  }

  async refresh() {
    if (!this.view) {
      return;
    }
    await refreshWebview(getWorkspaceRoot(), this.view.webview);
  }
}

async function openPanel(context) {
  const root = getWorkspaceRoot();

  if (currentPanel) {
    currentPanel.reveal(vscode.ViewColumn.Beside);
  } else {
    currentPanel = vscode.window.createWebviewPanel(
      "estudioExercism",
      "Estudio Socratico",
      vscode.ViewColumn.Beside,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
      },
    );
    currentPanel.onDidDispose(() => {
      currentPanel = undefined;
    }, null, context.subscriptions);

    currentPanel.webview.onDidReceiveMessage(async (message) => {
      await handleWebviewMessage(root, message, currentPanel.webview);
    }, undefined, context.subscriptions);
  }

  await refreshPanel(root);
}

async function refreshPanel(root) {
  if (!currentPanel) {
    return;
  }

  await refreshWebview(root, currentPanel.webview);
}

async function refreshWebview(root, webview) {
  webview.html = renderLoadingHtml();
  try {
    const catalog = await runManagerJson(root, ["-Action", "catalog"]);
    webview.html = renderCatalogHtml(catalog);
  } catch (error) {
    webview.html = renderErrorHtml(error.message);
  }
}

async function refreshAll(root) {
  await refreshPanel(root);
  if (currentProvider) {
    await currentProvider.refresh();
  }
}

async function handleWebviewMessage(root, message, sourceWebview) {
  try {
    if (message.command === "refresh") {
      await refreshWebview(root, sourceWebview);
      return;
    }

    if (message.command === "configureToken") {
      const terminal = vscode.window.createTerminal("Exercism - configurar token");
      terminal.show();
      terminal.sendText("exercism configure --token TU_TOKEN_AQUI");
      vscode.window.showInformationMessage("Cambia TU_TOKEN_AQUI por tu token de Exercism y ejecuta el comando.");
      return;
    }

    if (message.command === "import") {
      await importExercise(root, message.provider, message.slug);
      await refreshAll(root);
      return;
    }

    if (message.command === "mark") {
      await markExercise(root, message.provider, message.slug, message.status);
      await refreshAll(root);
      return;
    }

    if (message.command === "open" && message.folder) {
      const folderUri = vscode.Uri.file(message.folder);
      await vscode.commands.executeCommand("revealFileInOS", folderUri);
      return;
    }

    if (message.command === "test" && message.folder) {
      runInTerminal(root, "test", message.folder);
      return;
    }

    if (message.command === "submit" && message.folder) {
      runInTerminal(root, "submit", message.folder);
    }
  } catch (error) {
    vscode.window.showErrorMessage(cleanMessage(error.message));
  }
}

async function importExercise(root, provider, slug) {
  await vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: "Importando ejercicio",
      cancellable: false,
    },
    async () => {
      let result;
      try {
        result = await runManagerJson(root, ["-Action", "import", "-Provider", provider, "-Slug", slug]);
      } catch (error) {
        if (/Ya existe/i.test(error.message)) {
          const choice = await vscode.window.showWarningMessage(
            "Ese ejercicio ya existe en Ejercicios. ¿Quieres reemplazarlo?",
            { modal: true },
            "Reemplazar",
          );
          if (choice !== "Reemplazar") {
            return;
          }
          result = await runManagerJson(root, ["-Action", "import", "-Provider", provider, "-Slug", slug, "-Force"]);
        } else {
          throw error;
        }
      }

      if (!result || result.ok === false) {
        throw new Error(result && result.error ? result.error : "No se pudo importar el ejercicio.");
      }

      vscode.window.showInformationMessage(`Ejercicio importado: ${result.title}`);
      if (result.openFile) {
        const doc = await vscode.workspace.openTextDocument(vscode.Uri.file(result.openFile));
        await vscode.window.showTextDocument(doc, vscode.ViewColumn.One);
      }
    },
  );
}

async function markExercise(root, provider, slug, status) {
  const result = await runManagerJson(root, [
    "-Action",
    "mark",
    "-Provider",
    provider,
    "-Slug",
    slug,
    "-NewStatus",
    status,
  ]);

  if (!result || result.ok === false) {
    throw new Error(result && result.error ? result.error : "No se pudo actualizar el estado del ejercicio.");
  }

  const label = status === "completed" ? "completado" : "en progreso";
  vscode.window.showInformationMessage(`Ejercicio marcado como ${label}: ${result.title}`);
}

function runForCurrentFile(action) {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    vscode.window.showWarningMessage("Abre un archivo del ejercicio primero.");
    return;
  }
  const root = getWorkspaceRoot();
  runInTerminal(root, action, editor.document.uri.fsPath);
}

function runInTerminal(root, action, targetPath) {
  const terminalName = action === "submit" ? "Exercism Submit" : "Exercism Test";
  const terminal = vscode.window.createTerminal(terminalName);
  terminal.show();
  const command = [
    "powershell",
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    psQuote(getManagerPath(root)),
    "-RepoRoot",
    psQuote(root),
    "-Action",
    action,
    "-ExercisePath",
    psQuote(targetPath),
  ].join(" ");
  terminal.sendText(command);
}

function psQuote(value) {
  return `'${String(value).replace(/'/g, "''")}'`;
}

function cleanMessage(message) {
  return String(message || "").replace(/\s+/g, " ").trim();
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function renderLoadingHtml() {
  return baseHtml(`<main class="shell"><section class="loading">Cargando ejercicios...</section></main>`);
}

function renderErrorHtml(message) {
  return baseHtml(`
    <main class="shell">
      <h1>Estudio Socrático</h1>
      <section class="notice error">${escapeHtml(message)}</section>
      <button data-command="refresh">Reintentar</button>
    </main>
  `);
}

function renderCatalogHtml(catalog) {
  const exercises = Array.isArray(catalog.exercises) ? catalog.exercises : [];
  const tokenNotice = catalog.exercismCli && catalog.exercismCli.tokenConfigured
    ? ""
    : `<section class="notice">Exercism CLI no tiene token configurado. Puedes importar el catálogo público, pero para ver tu progreso real y enviar soluciones necesitas configurar tu token.</section>`;

  const cards = exercises.map(renderExerciseCard).join("");
  return baseHtml(`
    <main class="shell">
      <header class="topbar">
        <div>
          <p class="eyebrow">Estudio Socrático v1.0</p>
          <h1>Ejercicios</h1>
        </div>
        <div class="actions">
          <button data-command="refresh">Actualizar</button>
          <button data-command="configureToken">Configurar token</button>
        </div>
      </header>
      ${tokenNotice}
      <section class="statusFilters" aria-label="Filtrar por estado">
        <button class="statusFilter active" data-status="all"><span>Todos</span><strong data-count="all">0</strong></button>
        <button class="statusFilter" data-status="completed"><span>Completados</span><strong data-count="completed">0</strong></button>
        <button class="statusFilter" data-status="in_progress"><span>En progreso</span><strong data-count="in_progress">0</strong></button>
        <button class="statusFilter" data-status="available"><span>Disponibles</span><strong data-count="available">0</strong></button>
      </section>
      <section class="toolbar">
        <div class="providerFilters" aria-label="Filtrar por fuente">
          <button class="providerFilter active" data-provider="exercism">Exercism C</button>
          <button class="providerFilter" data-provider="learn-c">learn-c.org</button>
          <button class="providerFilter" data-provider="alejandro">PDF Alejandro</button>
          <button class="providerFilter" data-provider="all">Todo</button>
        </div>
        <input id="search" type="search" placeholder="Filtrar por titulo o tema" />
        <div class="themeSwitch" aria-label="Tema visual">
          <button class="themeButton active" data-theme="light">Claro</button>
          <button class="themeButton" data-theme="dark">Oscuro</button>
          <button class="themeButton" data-theme="system">VS Code</button>
        </div>
      </section>
      <section id="cards" class="cards">${cards}</section>
      <section id="emptyState" class="empty hidden">No hay ejercicios con esos filtros.</section>
    </main>
  `);
}

function renderExerciseCard(exercise) {
  const status = exercise.status || (exercise.imported ? "imported" : "available");
  const statusLabel = statusText(status);
  const statusGroup = statusGroupName(status);
  const topics = (exercise.topics || []).slice(0, 3).map((topic) => `<span>${escapeHtml(topic)}</span>`).join("");
  const icon = exercise.iconUrl
    ? `<img src="${escapeHtml(exercise.iconUrl)}" alt="" />`
    : `<div class="fallbackIcon">${escapeHtml(providerInitials(exercise))}</div>`;
  const folder = escapeHtml(exercise.folder || "");
  const canTest = exercise.imported && exercise.supportsTests;
  const canSubmit = exercise.imported && exercise.supportsSubmit;
  const canMarkDone = exercise.imported && exercise.provider !== "exercism" && statusGroup !== "completed";
  const canReopen = exercise.imported && exercise.provider !== "exercism" && statusGroup === "completed";
  const primaryCommand = exercise.imported && exercise.folder ? "open" : "import";
  const recommended = exercise.recommended ? `<span class="recommended">Recomendado</span>` : "";
  const search = `${exercise.title} ${exercise.blurb} ${(exercise.topics || []).join(" ")} ${exercise.providerName}`.toLowerCase();
  const difficulty = String(exercise.difficulty || "sin nivel").toLowerCase();
  const difficultyClass = ["easy", "medium", "hard"].includes(difficulty) ? difficulty : "unknown";

  return `
    <article class="exerciseCard"
      tabindex="0"
      role="button"
      aria-label="${escapeHtml(`${exercise.title}. ${statusLabel}`)}"
      data-command="${escapeHtml(primaryCommand)}"
      data-provider-action="${escapeHtml(exercise.provider)}"
      data-slug="${escapeHtml(exercise.slug)}"
      data-folder="${folder}"
      data-provider="${escapeHtml(exercise.provider)}"
      data-status="${escapeHtml(status)}"
      data-status-group="${escapeHtml(statusGroup)}"
      data-search="${escapeHtml(search)}">
      <div class="icon">${icon}</div>
      <div class="content">
        <div class="cardHeader">
          <h2>${escapeHtml(exercise.title)}</h2>
          <span class="provider">${escapeHtml(exercise.providerName)}</span>
        </div>
        <div class="badges">
          <span class="status ${escapeHtml(status)}">${escapeHtml(statusLabel)}</span>
          <span class="difficulty ${escapeHtml(difficultyClass)}">${escapeHtml(exercise.difficulty || "sin nivel")}</span>
          ${recommended}
        </div>
        <p>${escapeHtml(exercise.blurb || "")}</p>
        <div class="topics">${topics}</div>
      </div>
      <div class="cardActions">
        ${canTest ? `<button data-command="test" data-folder="${folder}">Probar</button>` : ""}
        ${canSubmit ? `<button data-command="submit" data-folder="${folder}">Enviar</button>` : ""}
        ${canMarkDone ? `<button data-command="mark" data-provider="${escapeHtml(exercise.provider)}" data-slug="${escapeHtml(exercise.slug)}" data-status="completed">Completar</button>` : ""}
        ${canReopen ? `<button data-command="mark" data-provider="${escapeHtml(exercise.provider)}" data-slug="${escapeHtml(exercise.slug)}" data-status="in_progress">Reabrir</button>` : ""}
      </div>
    </article>
  `;
}

function statusText(status) {
  const labels = {
    available: "Disponible",
    imported: "En progreso",
    tests_passed: "Tests OK",
    tests_failed: "Tests fallando",
    submitted: "Enviado",
    submit_failed: "Submit fallo",
    completed: "Completado",
    in_progress: "En progreso",
  };
  return labels[status] || status;
}

function statusGroupName(status) {
  if (["completed", "submitted"].includes(status)) {
    return "completed";
  }
  if (["imported", "tests_passed", "tests_failed", "submit_failed", "in_progress"].includes(status)) {
    return "in_progress";
  }
  return "available";
}

function providerInitials(exercise) {
  if (exercise.provider === "alejandro") {
    return "AL";
  }
  if (exercise.provider === "learn-c") {
    return "LC";
  }
  return "EX";
}

function baseHtml(body) {
  return `<!doctype html>
<html lang="es" data-theme="light">
<head>
  <meta charset="UTF-8" />
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src https: data:; style-src 'unsafe-inline'; script-src 'unsafe-inline';" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <style>
    :root {
      color-scheme: light;
      --bg: #f4f7ff;
      --panel: #ffffff;
      --fg: #130b43;
      --muted: #5c4f81;
      --soft: #786f98;
      --border: #e9edfb;
      --button: #eef3ff;
      --buttonFg: #110943;
      --accent: #5e2bff;
      --accentSoft: #e8eeff;
      --card: #ffffff;
      --cardHover: #fbfcff;
      --shadow: 0 16px 42px rgba(35, 26, 92, 0.08);
      --pill: #ffffff;
      --ok: #38b868;
      --okBg: #e9faee;
      --progress: #5d8cff;
      --progressBg: #eaf1ff;
      --warn: #d99b20;
      --warnBg: #fff5dd;
      --bad: #d55353;
      --badBg: #fff0f0;
      --font: "Segoe UI", "Aptos", system-ui, sans-serif;
    }
    html[data-theme="dark"] {
      color-scheme: dark;
      --bg: #15171e;
      --panel: #191c24;
      --fg: #f8f8ff;
      --muted: #c8cfdf;
      --soft: #929aae;
      --border: #2a2f3b;
      --button: #242a36;
      --buttonFg: #f8f8ff;
      --accent: #7b6cff;
      --accentSoft: #252545;
      --card: #10131a;
      --cardHover: #171b24;
      --shadow: 0 14px 36px rgba(0, 0, 0, 0.28);
      --pill: #151a23;
      --ok: #54d889;
      --okBg: #143520;
      --progress: #7aa2ff;
      --progressBg: #17284a;
      --warn: #f0b95a;
      --warnBg: #3b2a12;
      --bad: #ff7474;
      --badBg: #3f1b1d;
    }
    html[data-theme="system"] {
      color-scheme: light dark;
      --bg: var(--vscode-editor-background);
      --panel: var(--vscode-editor-background);
      --fg: var(--vscode-editor-foreground);
      --muted: var(--vscode-descriptionForeground);
      --soft: var(--vscode-descriptionForeground);
      --border: var(--vscode-panel-border);
      --button: var(--vscode-button-background);
      --buttonFg: var(--vscode-button-foreground);
      --accent: var(--vscode-focusBorder);
      --accentSoft: var(--vscode-list-hoverBackground);
      --card: var(--vscode-sideBar-background);
      --cardHover: var(--vscode-list-hoverBackground);
      --shadow: none;
      --pill: var(--vscode-editor-background);
      --ok: #35a852;
      --okBg: transparent;
      --progress: #5d8cff;
      --progressBg: transparent;
      --warn: #d99b20;
      --warnBg: transparent;
      --bad: #d55353;
      --badBg: transparent;
      --font: var(--vscode-font-family);
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: var(--font);
      background: var(--bg);
      color: var(--fg);
    }
    .shell {
      width: min(100%, 980px);
      margin: 0 auto;
      padding: 18px;
    }
    .topbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 14px;
      margin-bottom: 14px;
    }
    .eyebrow {
      color: var(--muted);
      margin: 0 0 4px;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0;
      font-weight: 700;
    }
    h1 { margin: 0; font-size: 26px; line-height: 1.1; }
    h2 { margin: 0; font-size: 20px; line-height: 1.2; }
    button {
      border: 1px solid transparent;
      border-radius: 8px;
      background: var(--button);
      color: var(--buttonFg);
      padding: 8px 12px;
      cursor: pointer;
      font: inherit;
      line-height: 1;
    }
    button:hover { border-color: var(--accent); }
    .actions, .toolbar, .providerFilters, .themeSwitch, .cardActions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
    }
    .actions { justify-content: flex-end; }
    .statusFilters {
      display: flex;
      flex-wrap: nowrap;
      overflow-x: auto;
      overflow-y: hidden;
      gap: 8px;
      margin: 10px 0 14px;
      padding-bottom: 6px;
      scrollbar-color: var(--accentSoft) transparent;
      scrollbar-width: thin;
    }
    .statusFilters::-webkit-scrollbar {
      height: 8px;
    }
    .statusFilters::-webkit-scrollbar-track {
      background: transparent;
      border-radius: 999px;
    }
    .statusFilters::-webkit-scrollbar-thumb {
      background: var(--accentSoft);
      border: 2px solid var(--bg);
      border-radius: 999px;
    }
    .statusFilter {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
      gap: 9px;
      border-radius: 999px;
      padding: 9px 16px;
      background: transparent;
      color: var(--muted);
      font-weight: 650;
    }
    .statusFilter strong {
      color: var(--soft);
      font-size: 13px;
    }
    .statusFilter.active {
      background: var(--accentSoft);
      color: var(--fg);
    }
    .statusFilter[data-status="completed"].active { color: var(--ok); }
    .statusFilter[data-status="in_progress"].active { color: var(--progress); }
    .toolbar {
      margin: 8px 0 16px;
      align-items: stretch;
    }
    .providerFilter, .themeButton {
      background: transparent;
      color: var(--fg);
      border-color: var(--border);
      border-radius: 7px;
    }
    .providerFilter.active, .themeButton.active {
      border-color: var(--accent);
      background: var(--accentSoft);
    }
    .themeSwitch {
      margin-left: auto;
    }
    input {
      min-width: 220px;
      flex: 1;
      border: 1px solid var(--border);
      background: var(--panel);
      color: var(--fg);
      padding: 9px 12px;
      border-radius: 7px;
      font: inherit;
    }
    .notice, .loading, .empty {
      border: 1px solid var(--border);
      background: var(--panel);
      padding: 14px;
      border-radius: 8px;
      margin: 14px 0;
      color: var(--muted);
    }
    .notice { border-color: var(--warn); color: var(--fg); }
    .notice.error { border-color: var(--bad); }
    .cards { display: grid; gap: 14px; }
    .exerciseCard {
      display: grid;
      grid-template-columns: 78px minmax(0, 1fr) auto;
      gap: 18px;
      align-items: center;
      border: 1px solid var(--border);
      background: var(--card);
      border-radius: 8px;
      padding: 18px 20px;
      box-shadow: var(--shadow);
      min-height: 124px;
      transition: border-color 120ms ease, transform 120ms ease, background 120ms ease;
    }
    .exerciseCard:hover, .exerciseCard:focus-visible {
      border-color: var(--accent);
      background: var(--cardHover);
      outline: none;
    }
    .exerciseCard:hover {
      transform: translateY(-1px);
    }
    .exerciseCard[data-status-group="completed"] {
      opacity: 0.78;
    }
    .icon img, .fallbackIcon {
      width: 64px;
      height: 64px;
      border-radius: 50%;
      display: grid;
      place-items: center;
      background: #c9f3ff;
      color: #100a43;
      font-weight: 700;
      object-fit: contain;
    }
    .fallbackIcon {
      border: 1px solid var(--border);
    }
    .cardHeader {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
    }
    .provider { color: var(--muted); font-size: 12px; white-space: nowrap; }
    .badges, .topics {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: 8px;
    }
    .badges span, .topics span {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: 1px solid var(--border);
      border-radius: 999px;
      min-height: 22px;
      padding: 0 10px;
      color: var(--muted);
      font-size: 12px;
      line-height: 1.1;
      background: var(--pill);
    }
    .difficulty.easy {
      border-color: var(--ok) !important;
      color: var(--ok) !important;
      background: var(--okBg) !important;
      font-weight: 650;
    }
    .difficulty.medium {
      border-color: var(--warn) !important;
      color: var(--warn) !important;
      background: var(--warnBg) !important;
      font-weight: 650;
    }
    .difficulty.hard {
      border-color: var(--bad) !important;
      color: var(--bad) !important;
      background: var(--badBg) !important;
      font-weight: 650;
    }
    .recommended { border-color: #aaa2c8 !important; color: var(--fg) !important; font-weight: 650; }
    .status.completed, .status.tests_passed, .status.submitted { border-color: var(--ok); color: var(--ok); }
    .status.in_progress { border-color: var(--progress); color: var(--progress); }
    .status.tests_failed, .status.submit_failed { border-color: var(--bad); color: var(--bad); }
    .status.imported { border-color: var(--warn); color: var(--warn); }
    p {
      color: var(--muted);
      line-height: 1.45;
      margin: 12px 0 0;
      font-size: 15px;
    }
    .cardActions {
      justify-content: flex-end;
      align-self: end;
    }
    .cardActions button {
      padding: 7px 10px;
      font-size: 12px;
      background: transparent;
      border-color: var(--border);
    }
    .hidden { display: none; }
    @media (max-width: 620px) {
      .shell { padding: 12px; }
      .topbar { align-items: flex-start; flex-direction: column; }
      .actions { justify-content: flex-start; }
      .themeSwitch { margin-left: 0; }
      .statusFilter { padding: 8px 12px; }
      .toolbar { gap: 10px; }
      .providerFilters, .themeSwitch { width: 100%; }
      input { min-width: 100%; }
      .exerciseCard {
        grid-template-columns: 58px minmax(0, 1fr);
        gap: 12px;
        padding: 14px;
        min-height: 108px;
      }
      .icon img, .fallbackIcon { width: 52px; height: 52px; }
      .cardHeader { flex-direction: column; gap: 4px; }
      .provider { white-space: normal; }
      h1 { font-size: 24px; }
      h2 { font-size: 18px; }
      p { font-size: 14px; }
      .cardActions {
        grid-column: 2;
        justify-content: flex-start;
      }
    }
  </style>
</head>
<body>
${body}
<script>
  const vscode = acquireVsCodeApi();
  const state = {
    provider: localStorage.getItem('estudio.provider') || 'exercism',
    status: localStorage.getItem('estudio.status') || 'all',
    theme: localStorage.getItem('estudio.theme') || 'light'
  };
  setTheme(state.theme);
  setActiveButtons();
  applyFilters();

  document.addEventListener('click', (event) => {
    const button = event.target.closest('button');
    if (button?.classList.contains('themeButton')) {
      state.theme = button.dataset.theme || 'light';
      localStorage.setItem('estudio.theme', state.theme);
      setTheme(state.theme);
      setActiveButtons();
      return;
    }
    if (button?.classList.contains('providerFilter')) {
      state.provider = button.dataset.provider || 'exercism';
      localStorage.setItem('estudio.provider', state.provider);
      setActiveButtons();
      applyFilters();
      return;
    }
    if (button?.classList.contains('statusFilter')) {
      state.status = button.dataset.status || 'all';
      localStorage.setItem('estudio.status', state.status);
      setActiveButtons();
      applyFilters();
      return;
    }
    if (button?.dataset.command) {
      event.stopPropagation();
      vscode.postMessage({
        command: button.dataset.command,
        provider: button.dataset.provider,
        slug: button.dataset.slug,
        folder: button.dataset.folder,
        status: button.dataset.status
      });
      return;
    }

    const card = event.target.closest('.exerciseCard');
    if (card) {
      postCardCommand(card);
    }
  });

  document.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    const card = event.target.closest('.exerciseCard');
    if (!card) return;
    event.preventDefault();
    postCardCommand(card);
  });

  document.getElementById('search')?.addEventListener('input', applyFilters);

  function postCardCommand(card) {
    const command = card.dataset.command;
    if (!command) return;
    vscode.postMessage({
      command,
      provider: card.dataset.providerAction,
      slug: card.dataset.slug,
      folder: card.dataset.folder
    });
  }

  function setTheme(theme) {
    document.documentElement.dataset.theme = ['light', 'dark', 'system'].includes(theme) ? theme : 'light';
  }

  function setActiveButtons() {
    document.querySelectorAll('.providerFilter').forEach((button) => {
      button.classList.toggle('active', button.dataset.provider === state.provider);
    });
    document.querySelectorAll('.statusFilter').forEach((button) => {
      button.classList.toggle('active', button.dataset.status === state.status);
    });
    document.querySelectorAll('.themeButton').forEach((button) => {
      button.classList.toggle('active', button.dataset.theme === state.theme);
    });
  }

  function applyFilters() {
    const query = (document.getElementById('search')?.value || '').trim().toLowerCase();
    let visible = 0;
    document.querySelectorAll('.card').forEach((card) => {
      card.classList.add('hidden');
    });
    document.querySelectorAll('.exerciseCard').forEach((card) => {
      const providerOk = state.provider === 'all' || card.dataset.provider === state.provider;
      const statusOk = state.status === 'all' || card.dataset.statusGroup === state.status;
      const queryOk = !query || (card.dataset.search || '').includes(query);
      const show = providerOk && statusOk && queryOk;
      card.classList.toggle('hidden', !show);
      if (show) visible += 1;
    });
    document.getElementById('emptyState')?.classList.toggle('hidden', visible !== 0);
    updateCounts();
  }

  function updateCounts() {
    const counts = { all: 0, completed: 0, in_progress: 0, available: 0 };
    document.querySelectorAll('.exerciseCard').forEach((card) => {
      const providerOk = state.provider === 'all' || card.dataset.provider === state.provider;
      if (!providerOk) return;
      counts.all += 1;
      const group = card.dataset.statusGroup || 'available';
      if (Object.prototype.hasOwnProperty.call(counts, group)) {
        counts[group] += 1;
      }
    });
    Object.entries(counts).forEach(([key, value]) => {
      document.querySelectorAll(\`[data-count="\${key}"]\`).forEach((item) => {
        item.textContent = value;
      });
    });
  }
</script>
</body>
</html>`;
}

module.exports = {
  activate,
  deactivate,
};
