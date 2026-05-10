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
    vscode.commands.registerCommand("estudioExercism.testCurrent", () => runForCurrentFile("test-window")),
    vscode.commands.registerCommand("estudioExercism.submitCurrent", () => runForCurrentFile("submit")),
    vscode.commands.registerCommand("estudioExercism.validateCurrent", () => runForCurrentFile("validate-window")),
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

    cp.execFile("powershell.exe", commandArgs, { cwd: root, maxBuffer: 1024 * 1024 * 30 }, (error, stdout, stderr) => {
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
        reject(new Error((stdout || stderr || error.message || "").trim()));
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
  } catch {
    throw new Error(`El backend no devolvio JSON valido: ${cleaned.slice(0, 1200)}`);
  }
}

function extractJson(text) {
  if (!text) return text;
  const starts = ["{", "["]
    .map((char) => ({ char, index: text.indexOf(char) }))
    .filter((item) => item.index >= 0)
    .sort((a, b) => a.index - b.index);
  if (starts.length === 0) return text;
  const start = starts[0].index;
  const close = text[start] === "{" ? "}" : "]";
  const end = text.lastIndexOf(close);
  return end > start ? text.slice(start, end + 1) : text.slice(start);
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
    if (this.view) {
      await refreshWebview(getWorkspaceRoot(), this.view.webview);
    }
  }
}

async function openPanel(context) {
  const root = getWorkspaceRoot();
  if (currentPanel) {
    currentPanel.reveal(vscode.ViewColumn.Beside);
  } else {
    currentPanel = vscode.window.createWebviewPanel("estudioExercism", "Estudio Socratico", vscode.ViewColumn.Beside, {
      enableScripts: true,
      retainContextWhenHidden: true,
    });
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
  if (currentPanel) {
    await refreshWebview(root, currentPanel.webview);
  }
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

    if (message.command === "open" && message.folder) {
      const uri = vscode.Uri.file(message.folder);
      await vscode.commands.executeCommand("revealFileInOS", uri);
      return;
    }

    if (message.command === "test" && message.folder) {
      await runManagerJson(root, ["-Action", "test-window", "-ExercisePath", message.folder]);
      return;
    }

    if (message.command === "validate" && message.folder) {
      await runManagerJson(root, ["-Action", "validate-window", "-ExercisePath", message.folder]);
      return;
    }

    if (message.command === "revealTests" && message.folder) {
      const result = await runManagerJson(root, ["-Action", "reveal-tests", "-ExercisePath", message.folder]);
      if (result && result.folder) {
        await vscode.commands.executeCommand("revealFileInOS", vscode.Uri.file(result.folder));
      }
      return;
    }

    if (message.command === "submit" && message.folder) {
      await submitExercise(root, message.folder);
      await refreshAll(root);
    }
  } catch (error) {
    vscode.window.showErrorMessage(cleanMessage(error.message));
  }
}

async function importExercise(root, provider, slug) {
  await vscode.window.withProgress(
    { location: vscode.ProgressLocation.Notification, title: "Importando ejercicio", cancellable: false },
    async () => {
      let result;
      try {
        result = await runManagerJson(root, ["-Action", "import", "-Provider", provider, "-Slug", slug]);
      } catch (error) {
        if (/Ya existe|already exists/i.test(error.message)) {
          const choice = await vscode.window.showWarningMessage(
            "Ese ejercicio ya existe en Ejercicios. Quieres reemplazarlo?",
            { modal: true },
            "Reemplazar",
          );
          if (choice !== "Reemplazar") return;
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

async function submitExercise(root, folder) {
  await vscode.window.withProgress(
    { location: vscode.ProgressLocation.Notification, title: "Enviando ejercicio a Exercism", cancellable: false },
    async () => {
      const result = await runManagerJson(root, ["-Action", "submit", "-ExercisePath", folder]);
      if (!result || result.ok === false) {
        const output = Array.isArray(result?.output) ? result.output.join(" ") : "";
        throw new Error(result?.error || output || "No se pudo enviar el ejercicio.");
      }
      if (result.completed) {
        const choice = await vscode.window.showInformationMessage(
          `Ejercicio completado en Exercism: ${result.title}`,
          result.viewUrl ? "Abrir Exercism" : undefined,
        );
        if (choice === "Abrir Exercism" && result.viewUrl) {
          vscode.env.openExternal(vscode.Uri.parse(result.viewUrl));
        }
        return;
      }

      const remoteStatus = result.remoteTestsStatus || result.status || "pendiente";
      const choice = await vscode.window.showWarningMessage(
        `Exercism recibio el envio, pero no lo marco como completado (${remoteStatus}). Revisa los tests remotos.`,
        result.viewUrl ? "Abrir Exercism" : undefined,
      );
      if (choice === "Abrir Exercism" && result.viewUrl) {
        vscode.env.openExternal(vscode.Uri.parse(result.viewUrl));
      }
    },
  );
}

function runForCurrentFile(action) {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    vscode.window.showWarningMessage("Abre un archivo del ejercicio primero.");
    return;
  }
  const root = getWorkspaceRoot();
  if (action === "test-window" || action === "validate-window") {
    runManagerJson(root, ["-Action", action, "-ExercisePath", editor.document.uri.fsPath]).catch((error) => {
      vscode.window.showErrorMessage(cleanMessage(error.message));
    });
    return;
  }
  if (action === "submit") {
    submitExercise(root, editor.document.uri.fsPath)
      .then(() => refreshAll(root))
      .catch((error) => vscode.window.showErrorMessage(cleanMessage(error.message)));
    return;
  }
  runInTerminal(root, action, editor.document.uri.fsPath);
}

function runInTerminal(root, action, targetPath) {
  const terminalName = action === "submit" ? "Exercism Submit" : "Estudio Ejercicios";
  const terminal = vscode.window.createTerminal(terminalName);
  terminal.show();
  terminal.sendText([
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
  ].join(" "));
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
      <h1>Estudio Socratico</h1>
      <section class="notice error">${escapeHtml(message)}</section>
      <button data-command="refresh">Reintentar</button>
    </main>
  `);
}

function renderCatalogHtml(catalog) {
  const exercises = normalizeExercises(catalog.exercises || []);
  const topics = [...new Set(exercises.flatMap((exercise) => exercise.topics || []))].sort((a, b) => a.localeCompare(b));
  const providers = [
    ["all", "Todas"],
    ["exercism", "Exercism C"],
    ["w3schools", "W3 / w3resource"],
    ["alejandro", "PDF Alejandro"],
  ];
  const tokenNotice = catalog.exercismCli && catalog.exercismCli.tokenConfigured
    ? ""
    : `<section class="notice">Exercism CLI no tiene token configurado. Configuralo para ver progreso real y enviar soluciones.</section>`;
  const cards = exercises.map(renderExerciseCard).join("");
  const topicButtons = topics.map((topic) => `
    <button class="topicToggle" data-topic="${escapeHtml(topic)}" data-topic-state="off" aria-pressed="false">
      <span class="topicMark" aria-hidden="true"></span>
      <span>${escapeHtml(topic)}</span>
    </button>
  `).join("");

  return baseHtml(`
    <main class="shell">
      <header class="topbar">
        <div>
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
          ${providers.map(([id, label]) => `<button class="providerFilter ${id === "all" ? "active" : ""}" data-provider="${id}">${label}</button>`).join("")}
        </div>
        <input id="search" type="search" placeholder="Filtrar por titulo o tema" />
        <button id="filterToggle" class="filterToggle" aria-expanded="false">
          <span>Filtros</span>
          <span class="chevron" aria-hidden="true">▾</span>
        </button>
        <div class="themeSwitch" aria-label="Tema visual">
          <button class="themeButton active" data-theme="light">Claro</button>
          <button class="themeButton" data-theme="dark">Oscuro</button>
          <button class="themeButton" data-theme="system">VS Code</button>
        </div>
      </section>
      <section id="filterPanel" class="filterPanel hidden">
        <div class="filterGroup">
          <h3>Dificultad</h3>
          <div class="filterList compact">
            <label class="check"><input type="checkbox" data-difficulty="easy" />Easy</label>
            <label class="check"><input type="checkbox" data-difficulty="medium" />Medium</label>
            <label class="check"><input type="checkbox" data-difficulty="hard" />Hard</label>
          </div>
        </div>
        <div class="filterGroup">
          <h3>Temas</h3>
          <div class="modeRow">
            <button class="logicButton active" data-logic="or">OR</button>
            <button class="logicButton" data-logic="and">AND</button>
          </div>
          <p class="filterHint">Un toque incluye el tema. Dos toques lo excluyen. Tres lo limpian.</p>
          <div class="topicList">${topicButtons || "<span class='muted'>Sin temas.</span>"}</div>
        </div>
      </section>
      <section id="cards" class="cards">${cards}</section>
      <section id="emptyState" class="empty hidden">No hay ejercicios con esos filtros.</section>
    </main>
  `);
}

function normalizeExercises(exercises) {
  const rank = { easy: 1, medium: 2, hard: 3 };
  return [...exercises].sort((a, b) => {
    const aGroup = statusGroupName(a.status || "available") === "completed" ? 1 : 0;
    const bGroup = statusGroupName(b.status || "available") === "completed" ? 1 : 0;
    if (aGroup !== bGroup) return aGroup - bGroup;
    if (a.provider === "exercism" && b.provider === "exercism") {
      if (a.unlocked !== b.unlocked) return a.unlocked ? -1 : 1;
      if (Boolean(a.recommended) !== Boolean(b.recommended)) return a.recommended ? -1 : 1;
    }
    const diff = (rank[String(a.difficulty || "").toLowerCase()] || 99) - (rank[String(b.difficulty || "").toLowerCase()] || 99);
    if (diff !== 0) return diff;
    return (a.order || 0) - (b.order || 0);
  });
}

function renderExerciseCard(exercise) {
  const status = exercise.status || (exercise.imported ? "in_progress" : "available");
  const statusLabel = exercise.unlocked === false ? "Bloqueado" : statusText(status);
  const statusGroup = exercise.unlocked === false ? "locked" : statusGroupName(status);
  const topics = (exercise.topics || []).slice(0, 5).map((topic) => `<span>${escapeHtml(topic)}</span>`).join("");
  const icon = exercise.iconUrl
    ? `<img src="${escapeHtml(exercise.iconUrl)}" alt="" />`
    : `<div class="fallbackIcon">${escapeHtml(providerInitials(exercise))}</div>`;
  const folder = escapeHtml(exercise.folder || "");
  const canImport = exercise.unlocked !== false && !exercise.imported;
  const primaryCommand = exercise.imported && exercise.folder ? "open" : (canImport ? "import" : "");
  const recommended = exercise.recommended ? `<span class="recommended">Recomendado</span>` : "";
  const difficulty = String(exercise.difficulty || "sin nivel").toLowerCase();
  const difficultyClass = ["easy", "medium", "hard"].includes(difficulty) ? difficulty : "unknown";
  const search = `${exercise.title} ${exercise.blurb} ${(exercise.topics || []).join(" ")} ${exercise.providerName}`.toLowerCase();
  const allTopics = (exercise.topics || []).join("|").toLowerCase();
  const actions = [];
  if (exercise.imported && exercise.supportsTests) actions.push(`<button data-command="test" data-folder="${folder}">Probar</button>`);
  if (exercise.imported && exercise.supportsValidate) actions.push(`<button data-command="validate" data-folder="${folder}">Validar</button>`);
  if (exercise.imported && exercise.supportsValidate) actions.push(`<button data-command="revealTests" data-folder="${folder}">Ver tests</button>`);
  if (exercise.imported && exercise.supportsSubmit) actions.push(`<button data-command="submit" data-folder="${folder}">Enviar</button>`);

  return `
    <article class="exerciseCard ${primaryCommand ? "" : "disabled"}"
      tabindex="${primaryCommand ? "0" : "-1"}"
      role="button"
      aria-label="${escapeHtml(`${exercise.title}. ${statusLabel}`)}"
      data-command="${escapeHtml(primaryCommand)}"
      data-provider-action="${escapeHtml(exercise.provider)}"
      data-slug="${escapeHtml(exercise.slug)}"
      data-folder="${folder}"
      data-provider="${escapeHtml(exercise.provider)}"
      data-status="${escapeHtml(status)}"
      data-status-group="${escapeHtml(statusGroup)}"
      data-difficulty="${escapeHtml(difficulty)}"
      data-topics="${escapeHtml(allTopics)}"
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
      <div class="cardActions">${actions.join("")}</div>
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
  if (["completed"].includes(status)) return "completed";
  if (["imported", "tests_passed", "tests_failed", "submitted", "submit_failed", "in_progress"].includes(status)) return "in_progress";
  return "available";
}

function providerInitials(exercise) {
  if (exercise.provider === "alejandro") return "AL";
  if (exercise.provider === "w3schools") return "W3";
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
      --border: #e4e9fb;
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
      --scrollTrack: #edf2ff;
      --scrollThumb: #c8d4ff;
      --scrollThumbHover: #aabaff;
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
      --scrollTrack: #10131a;
      --scrollThumb: #343b4e;
      --scrollThumbHover: #4b5571;
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
      --scrollTrack: transparent;
      --scrollThumb: var(--vscode-scrollbarSlider-background, rgba(120, 127, 145, 0.55));
      --scrollThumbHover: var(--vscode-scrollbarSlider-hoverBackground, rgba(120, 127, 145, 0.8));
      --font: var(--vscode-font-family);
    }
    * { box-sizing: border-box; }
    * { scrollbar-color: var(--scrollThumb) var(--scrollTrack); scrollbar-width: thin; }
    *::-webkit-scrollbar { width: 10px; height: 10px; }
    *::-webkit-scrollbar-track { background: var(--scrollTrack); border-radius: 999px; }
    *::-webkit-scrollbar-thumb { background: var(--scrollThumb); border: 2px solid var(--scrollTrack); border-radius: 999px; }
    *::-webkit-scrollbar-thumb:hover { background: var(--scrollThumbHover); }
    body { margin: 0; font-family: var(--font); background: var(--bg); color: var(--fg); }
    .shell { width: min(100%, 980px); margin: 0 auto; padding: 18px; }
    .topbar { display: flex; align-items: center; justify-content: space-between; gap: 14px; margin-bottom: 14px; }
    h1 { margin: 0; font-size: 26px; line-height: 1.1; }
    h2 { margin: 0; font-size: 20px; line-height: 1.2; }
    h3 { margin: 0 0 8px; font-size: 13px; color: var(--muted); }
    button {
      border: 1px solid transparent;
      border-radius: 8px;
      background: var(--button);
      color: var(--buttonFg);
      padding: 8px 12px;
      cursor: pointer;
      font: inherit;
      line-height: 1;
      white-space: nowrap;
    }
    button:hover { border-color: var(--accent); }
    .actions, .toolbar, .providerFilters, .themeSwitch, .cardActions, .modeRow { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
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
    .statusFilters::-webkit-scrollbar { height: 8px; }
    .statusFilter { flex: 0 0 auto; display: inline-flex; align-items: center; justify-content: center; gap: 9px; border-radius: 999px; padding: 9px 16px; background: transparent; color: var(--muted); font-weight: 650; }
    .statusFilter strong { color: var(--soft); font-size: 13px; }
    .statusFilter.active { background: var(--accentSoft); color: var(--fg); }
    .toolbar { display: grid; grid-template-columns: 1fr auto; gap: 10px; margin: 8px 0 16px; align-items: center; }
    .providerFilters { grid-column: 1 / -1; }
    input[type="search"] { grid-column: 1 / -1; min-width: 100%; border: 1px solid var(--border); background: var(--panel); color: var(--fg); padding: 9px 12px; border-radius: 7px; font: inherit; }
    .providerFilter, .themeButton, .filterToggle, .logicButton { background: transparent; color: var(--fg); border-color: var(--border); border-radius: 7px; }
    .providerFilter.active, .themeButton.active, .logicButton.active { border-color: var(--accent); background: var(--accentSoft); }
    .themeSwitch { justify-self: end; }
    .filterToggle { display: inline-flex; align-items: center; gap: 8px; justify-self: start; }
    .filterToggle .chevron { display: inline-block; line-height: 1; transition: transform 120ms ease; }
    .filterToggle.open .chevron { transform: rotate(180deg); }
    .filterPanel { border: 1px solid var(--border); background: var(--panel); border-radius: 8px; padding: 14px; margin: 0 0 16px; display: flex; flex-direction: column; gap: 14px; }
    .filterList { display: flex; flex-direction: column; align-items: flex-start; gap: 8px; }
    .filterList.compact { max-height: none; }
    .check { display: inline-flex; align-items: center; gap: 6px; border: 1px solid var(--border); border-radius: 999px; padding: 5px 9px; color: var(--muted); background: var(--pill); font-size: 12px; }
    .check input { accent-color: var(--accent); }
    .filterHint { margin: 8px 0 10px; font-size: 12px; color: var(--soft); }
    .topicList { display: flex; flex-direction: column; gap: 7px; max-height: 260px; overflow: auto; padding-right: 4px; }
    .topicToggle { width: 100%; display: inline-flex; align-items: center; justify-content: flex-start; gap: 9px; border-color: var(--border); background: var(--pill); color: var(--muted); text-align: left; }
    .topicToggle.include { border-color: var(--accent); background: var(--accentSoft); color: var(--fg); }
    .topicToggle.exclude { border-color: var(--bad); background: var(--badBg); color: var(--bad); }
    .topicMark { width: 12px; height: 12px; border: 1px solid currentColor; border-radius: 3px; flex: 0 0 auto; opacity: 0.85; }
    .topicToggle.include .topicMark { background: var(--accent); border-color: var(--accent); }
    .topicToggle.exclude .topicMark { background: var(--bad); border-color: var(--bad); }
    .notice, .loading, .empty { border: 1px solid var(--border); background: var(--panel); padding: 14px; border-radius: 8px; margin: 14px 0; color: var(--muted); }
    .notice { border-color: var(--warn); color: var(--fg); }
    .notice.error { border-color: var(--bad); }
    .cards { display: grid; gap: 14px; }
    .exerciseCard { display: grid; grid-template-columns: 78px minmax(0, 1fr) auto; gap: 18px; align-items: center; border: 1px solid var(--border); background: var(--card); border-radius: 8px; padding: 18px 20px; box-shadow: var(--shadow); min-height: 124px; transition: border-color 120ms ease, transform 120ms ease, background 120ms ease; }
    .exerciseCard:hover, .exerciseCard:focus-visible { border-color: var(--accent); background: var(--cardHover); outline: none; }
    .exerciseCard:hover { transform: translateY(-1px); }
    .exerciseCard.disabled { opacity: 0.62; cursor: default; }
    .exerciseCard[data-status-group="completed"] { opacity: 0.78; }
    .icon img, .fallbackIcon { width: 64px; height: 64px; border-radius: 50%; display: grid; place-items: center; background: #c9f3ff; color: #100a43; font-weight: 700; object-fit: contain; }
    .fallbackIcon { border: 1px solid var(--border); }
    .cardHeader { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
    .provider { color: var(--muted); font-size: 12px; white-space: nowrap; }
    .badges, .topics { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 8px; }
    .badges span, .topics span { display: inline-flex; align-items: center; justify-content: center; border: 1px solid var(--border); border-radius: 999px; min-height: 22px; padding: 0 10px; color: var(--muted); font-size: 12px; line-height: 1.1; background: var(--pill); }
    .difficulty.easy { border-color: var(--ok) !important; color: var(--ok) !important; background: var(--okBg) !important; font-weight: 650; }
    .difficulty.medium { border-color: var(--warn) !important; color: var(--warn) !important; background: var(--warnBg) !important; font-weight: 650; }
    .difficulty.hard { border-color: var(--bad) !important; color: var(--bad) !important; background: var(--badBg) !important; font-weight: 650; }
    .recommended { border-color: #aaa2c8 !important; color: var(--fg) !important; font-weight: 650; }
    .status.completed, .status.tests_passed, .status.submitted { border-color: var(--ok); color: var(--ok); }
    .status.in_progress, .status.imported { border-color: var(--progress); color: var(--progress); }
    .status.tests_failed, .status.submit_failed { border-color: var(--bad); color: var(--bad); }
    p { color: var(--muted); line-height: 1.45; margin: 12px 0 0; font-size: 15px; }
    .cardActions { justify-content: flex-end; align-self: end; }
    .cardActions button { padding: 7px 10px; font-size: 12px; background: transparent; border-color: var(--border); }
    .hidden { display: none !important; }
    .muted { color: var(--muted); font-size: 12px; }
    @media (max-width: 720px) {
      .shell { padding: 12px; }
      .topbar { align-items: flex-start; flex-direction: column; }
      .actions, .providerFilters, .themeSwitch { justify-content: flex-start; }
      .themeSwitch { justify-self: start; }
      input[type="search"] { min-width: 100%; }
      .exerciseCard { grid-template-columns: 58px minmax(0, 1fr); gap: 12px; padding: 14px; min-height: 108px; }
      .icon img, .fallbackIcon { width: 52px; height: 52px; }
      .cardHeader { flex-direction: column; gap: 4px; }
      .provider { white-space: normal; }
      h1 { font-size: 24px; }
      h2 { font-size: 18px; }
      p { font-size: 14px; }
      .cardActions { grid-column: 2; justify-content: flex-start; }
    }
  </style>
</head>
<body>
${body}
<script>
  const vscode = acquireVsCodeApi();
  const state = {
    provider: localStorage.getItem('estudio.provider') || 'all',
    status: localStorage.getItem('estudio.status') || 'all',
    theme: localStorage.getItem('estudio.theme') || 'light',
    logic: localStorage.getItem('estudio.logic') || 'or'
  };
  setTheme(state.theme);
  setActiveButtons();
  applyFilters();

  document.addEventListener('click', (event) => {
    const button = event.target.closest('button');
    if (button?.id === 'filterToggle') {
      const panel = document.getElementById('filterPanel');
      const isHidden = panel?.classList.toggle('hidden');
      button.classList.toggle('open', !isHidden);
      button.setAttribute('aria-expanded', String(!isHidden));
      return;
    }
    if (button?.classList.contains('topicToggle')) {
      cycleTopicButton(button);
      applyFilters();
      return;
    }
    if (button?.classList.contains('themeButton')) {
      state.theme = button.dataset.theme || 'light';
      localStorage.setItem('estudio.theme', state.theme);
      setTheme(state.theme);
      setActiveButtons();
      return;
    }
    if (button?.classList.contains('providerFilter')) {
      state.provider = button.dataset.provider || 'all';
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
    if (button?.classList.contains('logicButton')) {
      state.logic = button.dataset.logic || 'or';
      localStorage.setItem('estudio.logic', state.logic);
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
    if (card) postCardCommand(card);
  });

  document.addEventListener('change', (event) => {
    if (event.target.matches('input[type="checkbox"]')) applyFilters();
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
    vscode.postMessage({ command, provider: card.dataset.providerAction, slug: card.dataset.slug, folder: card.dataset.folder });
  }
  function setTheme(theme) {
    document.documentElement.dataset.theme = ['light', 'dark', 'system'].includes(theme) ? theme : 'light';
  }
  function setActiveButtons() {
    document.querySelectorAll('.providerFilter').forEach((button) => button.classList.toggle('active', button.dataset.provider === state.provider));
    document.querySelectorAll('.statusFilter').forEach((button) => button.classList.toggle('active', button.dataset.status === state.status));
    document.querySelectorAll('.themeButton').forEach((button) => button.classList.toggle('active', button.dataset.theme === state.theme));
    document.querySelectorAll('.logicButton').forEach((button) => button.classList.toggle('active', button.dataset.logic === state.logic));
  }
  function selectedValues(selector, attr) {
    return Array.from(document.querySelectorAll(selector + ':checked')).map((item) => item.dataset[attr]).filter(Boolean);
  }
  function selectedTopicValues(topicState) {
    return Array.from(document.querySelectorAll(\`.topicToggle[data-topic-state="\${topicState}"]\`))
      .map((item) => item.dataset.topic)
      .filter(Boolean);
  }
  function cycleTopicButton(button) {
    const current = button.dataset.topicState || 'off';
    const next = current === 'off' ? 'include' : (current === 'include' ? 'exclude' : 'off');
    button.dataset.topicState = next;
    button.classList.toggle('include', next === 'include');
    button.classList.toggle('exclude', next === 'exclude');
    button.setAttribute('aria-pressed', String(next !== 'off'));
    button.title = next === 'include'
      ? 'Incluido en el filtro'
      : (next === 'exclude' ? 'Excluido del filtro' : 'Sin filtro');
  }
  function applyFilters() {
    const query = (document.getElementById('search')?.value || '').trim().toLowerCase();
    const difficulties = selectedValues('input[data-difficulty]', 'difficulty');
    const includeTopics = selectedTopicValues('include').map((x) => x.toLowerCase());
    const excludeTopics = selectedTopicValues('exclude').map((x) => x.toLowerCase());
    let visible = 0;
    document.querySelectorAll('.exerciseCard').forEach((card) => {
      const topics = (card.dataset.topics || '').split('|').filter(Boolean);
      const providerOk = state.provider === 'all' || card.dataset.provider === state.provider;
      const statusOk = state.status === 'all' || card.dataset.statusGroup === state.status;
      const queryOk = !query || (card.dataset.search || '').includes(query);
      const difficultyOk = difficulties.length === 0 || difficulties.includes(card.dataset.difficulty || '');
      const includeOk = includeTopics.length === 0 || (state.logic === 'and'
        ? includeTopics.every((topic) => topics.includes(topic))
        : includeTopics.some((topic) => topics.includes(topic)));
      const excludeOk = excludeTopics.length === 0 || !excludeTopics.some((topic) => topics.includes(topic));
      const show = providerOk && statusOk && queryOk && difficultyOk && includeOk && excludeOk;
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
      if (Object.prototype.hasOwnProperty.call(counts, group)) counts[group] += 1;
    });
    Object.entries(counts).forEach(([key, value]) => {
      document.querySelectorAll(\`[data-count="\${key}"]\`).forEach((item) => { item.textContent = value; });
    });
  }
</script>
</body>
</html>`;
}

module.exports = { activate, deactivate };
