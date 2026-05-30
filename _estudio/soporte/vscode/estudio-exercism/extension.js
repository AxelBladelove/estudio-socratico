const vscode = require("vscode");
const cp = require("child_process");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const readline = require("readline");
const {
  ensureExtensionConfigFiles,
  getLocalConfigPath,
  getExampleConfigPath,
  readExtensionConfig,
} = require("./src/config");
const { analyzeExercise, formatAnalysisDryRun } = require("./src/ai/exerciseAnalysis");
const { describeAiTestGeneration } = require("./src/ai/testGeneration");

let currentPanel;
let currentProvider;
let aiOutputChannel;
let extensionBasePath = __dirname;
const engineClients = new Map();

function activate(context) {
  extensionBasePath = context.extensionPath || __dirname;
  currentProvider = new ExerciseViewProvider(context);
  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider("estudioExercism.view", currentProvider),
    vscode.commands.registerCommand("estudioExercism.compileActiveCFile", () => compileActiveCFile()),
    vscode.commands.registerCommand("estudioExercism.openPanel", () => openPanel(context)),
    vscode.commands.registerCommand("estudioExercism.openApiKeyConfig", () => openApiKeyConfig(getWorkspaceRoot())),
    vscode.commands.registerCommand("estudioExercism.revealApiKeyConfig", () => revealApiKeyConfig(getWorkspaceRoot())),
    vscode.commands.registerCommand("estudioExercism.testCurrent", () => runForCurrentFile("test-window")),
    vscode.commands.registerCommand("estudioExercism.submitCurrent", () => runForCurrentFile("submit")),
    vscode.commands.registerCommand("estudioExercism.validateCurrent", () => runForCurrentFile("validate-window")),
    vscode.commands.registerCommand("estudioExercism.analyzeExerciseWithAi", () => analyzeCurrentExerciseWithAi()),
    vscode.commands.registerCommand("estudioExercism.generateAiLogicTests", () => showAiTestGenerationStub()),
    vscode.window.registerUriHandler({
      handleUri: async (uri) => {
        const route = String(uri.path || "").toLowerCase();
        if (route.endsWith("/openpanel")) {
          await openPanel(context);
          return;
        }

        if (route.endsWith("/openapikeyconfig")) {
          await openApiKeyConfig(getWorkspaceRoot());
        }
      },
    }),
  );
}

function deactivate() {
  for (const client of engineClients.values()) {
    client.dispose();
  }
  engineClients.clear();
}

function getWorkspaceRoot() {
  const folder = vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders[0];
  if (!folder) {
    throw new Error("Abre primero la carpeta del repo Estudio Socratico en VS Code.");
  }
  return folder.uri.fsPath;
}

function getEnginePath() {
  const packagedEngine = path.join(extensionBasePath, "engine", "estudio-engine.exe");
  if (fs.existsSync(packagedEngine)) {
    return packagedEngine;
  }

  const devEngine = path.resolve(extensionBasePath, "..", "..", "engine", "bin", "estudio-engine.exe");
  if (fs.existsSync(devEngine)) {
    return devEngine;
  }

  return packagedEngine;
}

function getAssetsRoot() {
  return extensionBasePath;
}

async function compileActiveCFile() {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    throw new Error("Abre primero un archivo .c para compilarlo con F9.");
  }

  const activeFile = editor.document.uri.fsPath;
  if (path.extname(activeFile).toLowerCase() !== ".c") {
    throw new Error("F9 compila el archivo .c activo. Abre un archivo C y vuelve a intentar.");
  }

  if (editor.document.isDirty) {
    await editor.document.save();
  }

  const tasks = await vscode.tasks.fetchTasks({ type: "shell" });
  const estudioTask = tasks.find((task) => task.name === "Compilar y Grabar (Sistema Socratico)");
  if (estudioTask) {
    await vscode.tasks.executeTask(estudioTask);
    return;
  }

  const root = getWorkspaceRoot();
  const buildScript = path.join(root, "_estudio", "soporte", "scripts", "build.cmd");
  if (!fs.existsSync(buildScript)) {
    throw new Error("No se encontro _estudio/soporte/scripts/build.cmd en el workspace.");
  }

  const terminal = vscode.window.createTerminal({
    name: "Estudio Socratico",
    cwd: root,
    shellPath: "powershell.exe",
    shellArgs: ["-NoProfile"],
  });
  terminal.show();
  terminal.sendText(`& ${quotePowerShell(buildScript)} ${quotePowerShell(activeFile)}`);
}

function quotePowerShell(value) {
  return `'${String(value).replace(/'/g, "''")}'`;
}

async function openApiKeyConfig(root) {
  const { localConfigPath } = ensureExtensionConfigFiles(root);
  const doc = await vscode.workspace.openTextDocument(vscode.Uri.file(localConfigPath));
  await vscode.window.showTextDocument(doc, vscode.ViewColumn.One);
}

async function revealApiKeyConfig(root) {
  const { localConfigPath } = ensureExtensionConfigFiles(root);
  await vscode.commands.executeCommand("revealFileInOS", vscode.Uri.file(localConfigPath));
}

function runManagerJson(root, args) {
  const request = managerArgsToEngineRequest(args);
  return getEngineClient(root).request(request.method, request.params).then(async (result) => {
    if ((request.method === "test-window" || request.method === "validate-window") && result?.command) {
      runEngineCommandInTerminal(result);
    }
    return result;
  });
}

function getEngineClient(root) {
  const key = path.resolve(root).toLowerCase();
  let client = engineClients.get(key);
  if (!client) {
    client = new EngineClient(root);
    engineClients.set(key, client);
  }
  return client;
}

class EngineClient {
  constructor(root) {
    this.root = root;
    this.proc = undefined;
    this.reader = undefined;
    this.nextId = 1;
    this.pending = new Map();
    this.stderr = "";
  }

  request(method, params = {}) {
    this.start();
    const id = this.nextId++;
    const payload = JSON.stringify({ id, method, params });
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.proc.stdin.write(`${payload}\n`, "utf8", (error) => {
        if (error) {
          this.pending.delete(id);
          reject(error);
        }
      });
    });
  }

  start() {
    if (this.proc && !this.proc.killed) return;
    const engine = getEnginePath();
    if (!fs.existsSync(engine)) {
      throw new Error(`Falta el daemon Rust: ${engine}. Ejecuta _estudio\\soporte\\engine\\build-engine.cmd antes de empaquetar o instalar la extension.`);
    }

    this.stderr = "";
    this.proc = cp.spawn(engine, ["daemon", "--repo-root", this.root, "--assets-root", getAssetsRoot()], {
      cwd: this.root,
      stdio: ["pipe", "pipe", "pipe"],
      windowsHide: true,
    });
    this.reader = readline.createInterface({ input: this.proc.stdout });
    this.reader.on("line", (line) => this.handleLine(line));
    this.proc.stderr.on("data", (chunk) => {
      this.stderr += chunk.toString();
      if (this.stderr.length > 4000) this.stderr = this.stderr.slice(-4000);
    });
    this.proc.on("exit", () => {
      const error = new Error(cleanMessage(this.stderr) || "El daemon Rust se cerro.");
      for (const { reject } of this.pending.values()) reject(error);
      this.pending.clear();
      this.proc = undefined;
      this.reader = undefined;
    });
  }

  handleLine(line) {
    let message;
    try {
      message = JSON.parse(line);
    } catch {
      return;
    }
    const pending = this.pending.get(message.id);
    if (!pending) return;
    this.pending.delete(message.id);
    if (message.ok === false) {
      pending.reject(new Error(message.error || "El daemon Rust devolvio error."));
      return;
    }
    pending.resolve(message.result);
  }

  dispose() {
    if (this.reader) this.reader.close();
    if (this.proc) this.proc.kill();
    this.proc = undefined;
    this.reader = undefined;
  }
}

function managerArgsToEngineRequest(args) {
  const params = {};
  let method = "catalog";
  for (let index = 0; index < args.length; index += 1) {
    const raw = String(args[index] || "");
    if (!raw.startsWith("-")) continue;
    const key = raw.replace(/^-+/, "");
    const normalized = normalizeManagerArgName(key);
    const next = args[index + 1];
    const isFlag = next === undefined || String(next).startsWith("-");
    const value = isFlag ? true : next;
    if (!isFlag) index += 1;
    if (normalized === "action") {
      method = String(value).toLowerCase();
    } else {
      params[normalized] = value;
    }
  }
  return { method, params };
}

function normalizeManagerArgName(name) {
  const map = {
    Action: "action",
    Provider: "provider",
    Slug: "slug",
    NewStatus: "newStatus",
    File: "file",
    ExercisePath: "exercisePath",
    Force: "force",
    Json: "json",
  };
  return map[name] || name.charAt(0).toLowerCase() + name.slice(1);
}

function runEngineCommandInTerminal(result) {
  const command = result.command || {};
  const exe = command.exe;
  const args = Array.isArray(command.args) ? command.args : [];
  if (!exe) return;
  const terminal = vscode.window.createTerminal(result.title || "Estudio Ejercicios");
  terminal.show();
  terminal.sendText([cmdQuote(exe), ...args.map(cmdQuote)].join(" "));
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
    const [catalog, fundamentals] = await Promise.all([
      runManagerJson(root, ["-Action", "catalog"]),
      runManagerJson(root, ["-Action", "fundamentals.catalog"]),
    ]);
    const extensionConfig = readExtensionConfig(root);
    const apiKeyNotice = consumeApiKeyNotice(root, extensionConfig);
    webview.html = renderCatalogHtml(catalog, extensionConfig, apiKeyNotice, fundamentals);
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

    if (message.command === "openApiKeyConfig") {
      await openApiKeyConfig(root);
      return;
    }

    if (message.command === "revealApiKeyConfig") {
      await revealApiKeyConfig(root);
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
  const extensionConfig = readExtensionConfig(root);
  if (provider === "exercism" && extensionConfig.features.importExercism === false) {
    throw new Error("La importacion de Exercism esta desactivada en usuario/config/estudio-socratico.extension.local.json.");
  }
  if (provider === "alejandro" && extensionConfig.features.importAlejandroGists === false) {
    throw new Error("La importacion de Gists esta desactivada en usuario/config/estudio-socratico.extension.local.json.");
  }

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
      await analyzeImportedExerciseIfEnabled(root, result.folder);
      if (result.openFile) {
        const doc = await vscode.workspace.openTextDocument(vscode.Uri.file(result.openFile));
        await vscode.window.showTextDocument(doc, vscode.ViewColumn.One);
      }
    },
  );
}

async function analyzeImportedExerciseIfEnabled(root, exerciseFolder) {
  const extensionConfig = readExtensionConfig(root);
  if (!extensionConfig.features?.aiExerciseAnalysis || !exerciseFolder) {
    return;
  }

  try {
    const autoApply =
      extensionConfig.experimental?.applyHeaderPatchesAutomatically ||
      extensionConfig.experimental?.appendInstructionHintsAutomatically;
    const analysis = await analyzeExercise(root, exerciseFolder, extensionConfig, { dryRun: !autoApply });
    showAiAnalysisOutput(formatAnalysisDryRun(analysis));
    if (autoApply) {
      vscode.window.showInformationMessage("Analisis IA experimental completado. Revisa el canal de salida de Estudio Socratico.");
    } else {
      vscode.window.showInformationMessage("Analisis IA experimental listo en modo dry-run. No se modificaron archivos.");
    }
  } catch (error) {
    showAiAnalysisOutput(`Analisis IA experimental no disponible.\n\n${cleanMessage(error.message)}`);
    vscode.window.showWarningMessage("La importacion termino, pero el analisis IA experimental no pudo ejecutarse.");
  }
}

async function analyzeCurrentExerciseWithAi() {
  const root = getWorkspaceRoot();
  const extensionConfig = readExtensionConfig(root);
  if (!extensionConfig.features?.aiExerciseAnalysis) {
    const choice = await vscode.window.showInformationMessage(
      "El analisis IA experimental esta apagado. Activa features.aiExerciseAnalysis en la config local.",
      "Abrir config",
    );
    if (choice === "Abrir config") {
      await openApiKeyConfig(root);
    }
    return;
  }

  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    vscode.window.showWarningMessage("Abre un archivo del ejercicio antes de analizarlo con IA.");
    return;
  }

  await vscode.window.withProgress(
    { location: vscode.ProgressLocation.Notification, title: "Analizando ejercicio con IA (experimental)", cancellable: false },
    async () => {
      const analysis = await analyzeExercise(root, editor.document.uri.fsPath, extensionConfig, { dryRun: true });
      showAiAnalysisOutput(formatAnalysisDryRun(analysis));
    },
  );
}

function showAiTestGenerationStub() {
  const root = getWorkspaceRoot();
  const extensionConfig = readExtensionConfig(root);
  showAiAnalysisOutput(`Estudio Socratico 2.5 - Tests IA experimental\n\n${describeAiTestGeneration(extensionConfig)}`);
}

function showAiAnalysisOutput(text) {
  if (!aiOutputChannel) {
    aiOutputChannel = vscode.window.createOutputChannel("Estudio Socratico IA");
  }
  aiOutputChannel.clear();
  aiOutputChannel.appendLine(text);
  aiOutputChannel.show(true);
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
    cmdQuote(getEnginePath()),
    action,
    "--repo-root",
    cmdQuote(root),
    "--assets-root",
    cmdQuote(getAssetsRoot()),
    "--exercise-path",
    cmdQuote(targetPath),
  ].join(" "));
}

function cmdQuote(value) {
  return `"${String(value).replace(/"/g, '\\"')}"`;
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
      <div class="actions">
        <button data-command="refresh">Reintentar</button>
        <button data-command="openApiKeyConfig">Abrir configuración de API Key</button>
        <button data-command="revealApiKeyConfig">Revelar configuración</button>
      </div>
    </main>
  `);
}

function getApiKeyNoticeStorageKey(root) {
  const workspaceHash = crypto.createHash("sha1").update(String(root || "")).digest("hex");
  return `estudioExercism.apiKeyNotice.${workspaceHash}`;
}

function getApiKeyFingerprint(extensionConfig) {
  const apiKey = String(extensionConfig?.apiKey || "").trim();
  if (!apiKey) return "";
  const payload = JSON.stringify({
    provider: String(extensionConfig?.provider || "gemini"),
    model: String(extensionConfig?.model || ""),
    apiKey,
  });
  return crypto.createHash("sha256").update(payload).digest("hex");
}

function consumeApiKeyNotice(root, extensionConfig) {
  const hasApiKey = Boolean(String(extensionConfig?.apiKey || "").trim());
  if (!hasApiKey) {
    return `<section class="notice">No hay API Key local todavia. La extension seguira funcionando parcialmente sin ella.</section>`;
  }

  if (!currentProvider?.context) {
    return "";
  }

  const storageKey = getApiKeyNoticeStorageKey(root);
  const fingerprint = getApiKeyFingerprint(extensionConfig);
  const seenFingerprint = currentProvider.context.workspaceState.get(storageKey, "");
  if (fingerprint && fingerprint !== seenFingerprint) {
    void currentProvider.context.workspaceState.update(storageKey, fingerprint);
    return `<section class="notice success">API Key local detectada para ${escapeHtml(extensionConfig.provider || "gemini")}.</section>`;
  }

  return "";
}

function renderCatalogHtml(catalog, extensionConfig, apiKeyNotice, fundamentals = {}) {
  const exercises = normalizeExercises(catalog.exercises || []);
  const fundamentalsExercises = normalizeFundamentalsItems(fundamentals.exercises || []);
  const fundamentalsProjects = normalizeFundamentalsItems(fundamentals.projects || []);
  const fundamentalsQuizzes = normalizeFundamentalsItems(fundamentals.quizzes || []);
  const topics = [...new Set([
    ...exercises.flatMap((exercise) => exercise.topics || []),
    ...fundamentalsExercises.flatMap((item) => item.topics || []),
    ...fundamentalsProjects.flatMap((item) => item.topics || []),
    ...fundamentalsQuizzes.flatMap((item) => item.topics || []),
  ])].sort((a, b) => a.localeCompare(b));
  const providers = [
    ["all", "Todas"],
    ["exercism", "Exercism C"],
    ["alejandro", "PDF Alejandro"],
  ];
  const tokenNotice = catalog.exercismCli && catalog.exercismCli.tokenConfigured
    ? ""
    : `<section class="notice">Exercism CLI no tiene token configurado. Configuralo para ver progreso real y enviar soluciones.</section>`;
  const cards = exercises.map(renderExerciseCard).join("");
  const routeHtml = renderFundamentalsRoute(fundamentals.route || {});
  const quizCards = fundamentalsQuizzes.map((item) => renderFundamentalsCard(item, "quiz")).join("");
  const projectCards = fundamentalsProjects.map((item) => renderFundamentalsCard(item, "project")).join("");
  const fundamentalsCards = fundamentalsExercises.map((item) => renderFundamentalsCard(item, "exercise")).join("");
  const exerciseKinds = [["practice", "Practica"], ["masteryChallenge", "Reto"], ["combined", "Combinado"]];
  const projectKinds = [["assignment", "Asignacion"], ["project", "Proyecto"]];
  const quizKinds = [["tema", "Tema"], ["combinado", "Combinado"], ["acumulativo", "Acumulativo"], ["examen", "Examen"]];
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
          <button data-view="home">Inicio</button>
          <button data-view="exercism">Exercism</button>
          <button data-view="fundamentals">Estudio Socratico</button>
          <button data-command="refresh">Actualizar</button>
          <button data-command="configureToken">Configurar token</button>
          <button data-command="openApiKeyConfig">Abrir configuración de API Key</button>
          <button data-command="revealApiKeyConfig">Revelar configuración</button>
        </div>
      </header>
      ${tokenNotice}
      ${apiKeyNotice}
      <section class="viewSection" data-view-section="home">
        <div class="rootGrid">
          <article class="rootCard" data-view="exercism" tabindex="0" role="button">
            <div class="fallbackIcon">EX</div>
            <div>
              <h2>Exercism</h2>
              <p>Ejercicios oficiales del track C, importación, tests y submit.</p>
            </div>
          </article>
          <article class="rootCard" data-view="fundamentals" tabindex="0" role="button">
            <div class="fallbackIcon">ES</div>
            <div>
              <h2>Estudio Socratico</h2>
              <p>Ruta propia, quizzes teoricos, proyectos y practica por categoria.</p>
            </div>
          </article>
        </div>
      </section>

      <section class="viewSection hidden" data-view-section="fundamentals">
        <div class="rootGrid compactRoots">
          <article class="rootCard" data-view="fundamentals-categories" tabindex="0" role="button"><div class="fallbackIcon">EC</div><div><h2>Ejercicios por categoria</h2><p>Ejercicios puros y combinados con OR/AND.</p></div></article>
          <article class="rootCard" data-view="fundamentals-projects" tabindex="0" role="button"><div class="fallbackIcon">AP</div><div><h2>Asignaciones y proyectos</h2><p>Practica integradora filtrable.</p></div></article>
          <article class="rootCard" data-view="fundamentals-quizzes" tabindex="0" role="button"><div class="fallbackIcon">QT</div><div><h2>Quizzes teoricos</h2><p>Preguntas cortas por concepto.</p></div></article>
          <article class="rootCard" data-view="fundamentals-route" tabindex="0" role="button"><div class="fallbackIcon">RC</div><div><h2>Ruta C</h2><p>Preview progresivo; vista visual despues.</p></div></article>
        </div>
      </section>

      <section class="viewSection hidden" data-view-section="exercism">
        ${renderFilters({ providers, topicButtons, includeStatus: true })}
        <section id="cards" class="cards">${cards}</section>
        <section class="empty hidden">No hay ejercicios con esos filtros.</section>
      </section>

      <section class="viewSection hidden" data-view-section="fundamentals-route">
        <div class="subhead"><button data-view="fundamentals">Volver</button><h2>Ruta C</h2></div>
        ${routeHtml}
      </section>

      <section class="viewSection hidden" data-view-section="fundamentals-quizzes">
        <div class="subhead"><button data-view="fundamentals">Volver</button><h2>Quizzes teoricos</h2></div>
        ${renderFilters({ providers: [["fundamentals", "Fundamentos C"]], topicButtons, includeStatus: false, kindOptions: quizKinds, kindTitle: "Tipo de quiz" })}
        <section class="cards">${quizCards || "<section class='empty'>No hay quizzes todavia.</section>"}</section>
        <section class="empty hidden">No hay quizzes con esos filtros.</section>
      </section>

      <section class="viewSection hidden" data-view-section="fundamentals-projects">
        <div class="subhead"><button data-view="fundamentals">Volver</button><h2>Asignaciones y proyectos</h2></div>
        ${renderFilters({ providers: [["fundamentals", "Fundamentos C"]], topicButtons, includeStatus: false, kindOptions: projectKinds, kindTitle: "Tipo" })}
        <section class="cards">${projectCards || "<section class='empty'>No hay proyectos todavia.</section>"}</section>
        <section class="empty hidden">No hay proyectos con esos filtros.</section>
      </section>

      <section class="viewSection hidden" data-view-section="fundamentals-categories">
        <div class="subhead"><button data-view="fundamentals">Volver</button><h2>Ejercicios por categoria</h2></div>
        ${renderFilters({ providers: [["fundamentals", "Fundamentos C"]], topicButtons, includeStatus: false, kindOptions: exerciseKinds, kindTitle: "Tipo de ejercicio" })}
        <section class="cards">${fundamentalsCards || "<section class='empty'>No hay ejercicios todavia.</section>"}</section>
        <section class="empty hidden">No hay ejercicios con esos filtros.</section>
      </section>
    </main>
  `);
}

function renderFilters({ providers, topicButtons, includeStatus, kindOptions = [], kindTitle = "Tipo" }) {
  return `
    ${includeStatus ? `<section class="statusFilters" aria-label="Filtrar por estado">
      <button class="statusFilter active" data-status="all"><span>Todos</span><strong data-count="all">0</strong></button>
      <button class="statusFilter" data-status="completed"><span>Completados</span><strong data-count="completed">0</strong></button>
      <button class="statusFilter" data-status="in_progress"><span>En progreso</span><strong data-count="in_progress">0</strong></button>
      <button class="statusFilter" data-status="available"><span>Disponibles</span><strong data-count="available">0</strong></button>
    </section>` : ""}
    <section class="toolbar">
      <div class="providerFilters" aria-label="Filtrar por fuente">
        ${providers.map(([id, label], index) => `<button class="providerFilter ${index === 0 ? "active" : ""}" data-provider="${id}">${label}</button>`).join("")}
      </div>
      <input class="searchBox" type="search" placeholder="Filtrar por titulo o tema" />
      <button class="filterToggle" aria-expanded="false">
        <span>Filtros</span>
        <span class="chevron" aria-hidden="true">▾</span>
      </button>
      <div class="themeSwitch" aria-label="Tema visual">
        <button class="themeButton active" data-theme="light">Claro</button>
        <button class="themeButton" data-theme="dark">Oscuro</button>
        <button class="themeButton" data-theme="system">VS Code</button>
      </div>
    </section>
    <section class="filterPanel hidden">
      ${kindOptions.length ? `<div class="filterGroup">
        <h3>${escapeHtml(kindTitle)}</h3>
        <div class="filterList compact">
          ${kindOptions.map(([id, label]) => `<label class="check"><input type="checkbox" data-kind="${escapeHtml(id)}" />${escapeHtml(label)}</label>`).join("")}
        </div>
      </div>` : ""}
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
  `;
}

function normalizeFundamentalsItems(items) {
  return [...items].map((item) => ({
    ...item,
    provider: "fundamentals",
    providerName: "Fundamentos C",
    difficulty: typeof item.difficulty === "object" ? item.difficulty.band : item.difficulty,
    topics: item.topics || [...new Set([...(item.primaryTopics || []), ...(item.supportTopics || []), ...(item.combinedTopics || [])])],
    status: item.status || "not_started",
    kind: item.kind || item.quizType || "practice",
  }));
}

function renderFundamentalsRoute(route) {
  const modules = route.modules || [];
  const firstActive = modules.find((module) => module.unlocked) || modules[0] || {};
  return `<section class="routePath routeRoadmap">
    <div class="routeTabs" aria-label="Modulos de Ruta C">
      ${modules.map((module) => {
        const theme = routeTheme(module);
        return `<button class="routeTab ${module.id === firstActive.id ? "active" : ""}" data-route-target="${escapeHtml(module.id || "")}" style="--route-accent:${theme.accent};--route-glow:${theme.glow};">
          <span>${escapeHtml(module.title || "Modulo")}</span>
        </button>`;
      }).join("")}
    </div>
    ${modules.map((module) => renderRouteModuleCard(module, module.id === firstActive.id)).join("")}
  </section>`;
}

function renderRouteModuleCard(module, active) {
  const nodes = module.nodes || [];
  const decorated = decorateRouteNodes(nodes);
  const theme = routeTheme(module);
  const completed = decorated.filter((node) => node.routeState === "completed").length;
  const total = Math.max(decorated.length, 1);
  const percent = Math.round((completed / total) * 100);
  const next = decorated.find((node) => node.routeState === "current") || decorated.find((node) => node.routeState === "available") || decorated[0];
  const defaultDetail = next || decorated[0] || {};
  return `
    <article class="routeModuleCard ${active ? "active" : "hidden"} ${module.unlocked ? "" : "locked"} theme-${escapeHtml(theme.name)}"
      data-route-module="${escapeHtml(module.id || "")}"
      style="--route-accent:${theme.accent};--route-accent-2:${theme.accent2};--route-glow:${theme.glow};">
      <header class="routeHero">
        <div>
          <span class="routeEyebrow">Modulo ${escapeHtml(String(module.order || ""))}</span>
          <h3>${escapeHtml(module.title || "Ruta C")}</h3>
          <p>${escapeHtml((module.concepts || []).join(" · ") || "Fundamentos C")}</p>
        </div>
        <button class="routeGuideButton" disabled>Guia</button>
      </header>
      <div class="routeProgress">
        <span>${completed}/${total} pasos</span>
        <div class="routeSegments" aria-label="${escapeHtml(`${percent}% completado`)}">
          ${decorated.map((node) => `<span class="${node.routeState === "completed" ? "done" : (node.routeState === "current" ? "current" : "")}"></span>`).join("")}
        </div>
        <span>${percent}% completado</span>
      </div>
      <div class="routeBody">
        <div class="snakeWrap">
          <svg class="snakeLine" viewBox="0 0 120 ${Math.max(260, decorated.length * 88)}" preserveAspectRatio="none" aria-hidden="true">
            <path d="${escapeHtml(snakePath(decorated.length))}" />
          </svg>
          <div class="snakeNodes">
            ${decorated.map((node, index) => renderRouteNode(node, index)).join("")}
          </div>
        </div>
        <div class="routeNodeList">
          ${decorated.map((node) => `
            <button class="routeListItem ${(node.ref || node.id) === (defaultDetail.ref || defaultDetail.id) ? "active" : ""}" data-route-node-ref="${escapeHtml(node.ref || node.id || "")}">
              <span>${escapeHtml(nodeIcon(node))}</span>
              <strong>${escapeHtml(node.title || node.item?.title || "")}</strong>
              <small>${escapeHtml(routeNodeLabel(node))}</small>
            </button>
          `).join("")}
        </div>
        <aside class="routeVisual">
          ${routeIllustration(theme)}
          <div class="routeDetail" data-route-detail>
            ${renderRouteDetail(defaultDetail)}
          </div>
        </aside>
      </div>
      <footer class="routeFooter">
        <span>Proximo paso: ${escapeHtml(next?.title || next?.item?.title || "Completa los requisitos previos")}</span>
        <div class="routeLegend" aria-label="Estados de la ruta">
          <span><i class="legendNode completed">✓</i>Completado</span>
          <span><i class="legendNode current">&lt;/&gt;</i>Actual</span>
          <span><i class="legendNode locked">🔒</i>Bloqueado</span>
          <span><i class="legendNode challenge">★</i>Reto</span>
        </div>
        <button disabled>${next?.routeState === "locked" ? "Bloqueado" : "Sigue aqui"}</button>
      </footer>
    </article>
  `;
}

function decorateRouteNodes(nodes) {
  const completedStatuses = new Set(["completed", "tests_passed", "submitted"]);
  let currentAssigned = false;
  return nodes.map((node) => {
    const special = isSpecialRouteNode(node);
    let routeState = "locked";
    if (completedStatuses.has(node.status)) {
      routeState = "completed";
    } else if (node.unlocked) {
      routeState = currentAssigned ? "available" : "current";
      currentAssigned = true;
    }
    return { ...node, routeState, special };
  });
}

function renderRouteNode(node, index) {
  const side = index % 2 === 0 ? "left" : "right";
  const top = 18 + index * 86;
  const title = node.title || node.item?.title || "";
  const description = node.item?.description || node.item?.blurb || "";
  return `
    <button class="snakeNode ${side} ${escapeHtml(node.routeState)} ${node.special ? "challenge" : ""}"
      style="top:${top}px"
      data-route-node-ref="${escapeHtml(node.ref || node.id || "")}"
      data-route-title="${escapeHtml(title)}"
      data-route-description="${escapeHtml(description)}"
      data-route-label="${escapeHtml(routeNodeLabel(node))}"
      data-route-state="${escapeHtml(node.routeState)}"
      data-route-lock="${escapeHtml(node.routeState === "locked" ? "Completa los pasos anteriores." : "")}"
      aria-label="${escapeHtml(`${title}. ${routeNodeLabel(node)}`)}">
      <span>${escapeHtml(nodeIcon(node))}</span>
    </button>
  `;
}

function renderRouteDetail(node) {
  const title = node.title || node.item?.title || "Selecciona un nodo";
  const label = node.ref ? routeNodeLabel(node) : "Detalle";
  const state = node.routeState || "available";
  const description = node.item?.description || node.item?.blurb || (state === "locked" ? "Completa los pasos anteriores para desbloquear este nodo." : "Listo para continuar cuando quieras.");
  const action = state === "locked" ? "Bloqueado" : "Iniciar";
  return `
    <span class="routeDetailState">${escapeHtml(statusText(state))}</span>
    <h4>${escapeHtml(title)}</h4>
    <p>${escapeHtml(description)}</p>
    <div class="routeDetailMeta">${escapeHtml(label)}</div>
    <button disabled>${escapeHtml(action)}</button>
  `;
}

function snakePath(count) {
  const steps = Math.max(count, 2);
  let d = "M60 24";
  for (let index = 1; index < steps; index += 1) {
    const y = 24 + index * 86;
    const x = index % 2 === 0 ? 46 : 74;
    d += ` C60 ${y - 48}, ${x} ${y - 38}, ${x} ${y}`;
  }
  return d;
}

function routeTheme(module) {
  const concepts = ((module.concepts || []).join(" ") + " " + (module.title || "")).toLowerCase();
  if (concepts.includes("string") || concepts.includes("cadena")) return { name: "teal", accent: "#12d6ad", accent2: "#0aa084", glow: "rgba(18,214,173,.36)" };
  if (concepts.includes("matriz") || concepts.includes("matrix") || concepts.includes("array") || concepts.includes("arreglo")) return { name: "blue", accent: "#1e9bff", accent2: "#1469ff", glow: "rgba(30,155,255,.34)" };
  if (concepts.includes("memoria") || concepts.includes("pointer") || concepts.includes("puntero")) return { name: "orange", accent: "#ff980e", accent2: "#f35c00", glow: "rgba(255,152,14,.34)" };
  if (concepts.includes("struct") || concepts.includes("archivo")) return { name: "purple", accent: "#a970ff", accent2: "#6f3bdc", glow: "rgba(169,112,255,.34)" };
  if (concepts.includes("cond") || concepts.includes("loop") || concepts.includes("ciclo")) return { name: "orange", accent: "#ffb02e", accent2: "#f06d00", glow: "rgba(255,176,46,.28)" };
  return { name: "teal", accent: "#18c7bd", accent2: "#2f6bff", glow: "rgba(24,199,189,.28)" };
}

function routeIllustration(theme) {
  return `<svg class="routeIllustration" viewBox="0 0 160 150" role="img" aria-label="Ilustracion conceptual de modulo">
    <rect x="28" y="34" width="84" height="58" rx="12" fill="none" stroke="var(--route-accent)" stroke-width="3"/>
    <path d="M44 54h28M44 72h48M44 90h34" stroke="var(--route-accent)" stroke-width="3" stroke-linecap="round"/>
    <path d="M112 86c24 2 34 14 30 31-4 18-28 18-42 8" fill="none" stroke="var(--route-accent)" stroke-width="3" stroke-dasharray="5 6"/>
    <circle cx="114" cy="47" r="12" fill="var(--route-accent)" opacity=".2"/>
    <path d="M113 39v16M105 47h16" stroke="var(--route-accent)" stroke-width="3" stroke-linecap="round"/>
    <path d="M28 118c18-18 36-18 54 0 17 17 34 17 51 0" fill="none" stroke="var(--route-accent)" stroke-width="3"/>
  </svg>`;
}

function nodeIcon(node) {
  if (node.routeState === "completed") return "✓";
  if (node.routeState === "locked") return "🔒";
  if (node.special) return "★";
  if (node.type === "quiz") return "?";
  if (node.type === "project") return "P";
  return "</>";
}

function isSpecialRouteNode(node) {
  return ["challenge", "masteryChallenge", "checkpoint", "project"].includes(node.type) || node.item?.kind === "masteryChallenge";
}

function routeNodeLabel(node) {
  const labels = {
    exercise: "Ejercicio",
    quiz: "Quiz",
    checkpoint: "Checkpoint",
    masteryChallenge: "Reto de dominio",
    project: "Proyecto",
  };
  const status = node.status && node.status !== "not_started" ? ` · ${statusText(node.status)}` : "";
  return `${labels[node.type] || node.type || "Nodo"}${status}`;
}

function renderFundamentalsCard(item, kind) {
  const status = item.unlocked === false ? "locked" : (item.status || "not_started");
  const statusGroup = item.unlocked === false ? "locked" : statusGroupName(status);
  const topics = (item.topics || []).slice(0, 6).map((topic) => `<span>${escapeHtml(topic)}</span>`).join("");
  const allTopics = (item.topics || []).join("|").toLowerCase();
  const itemKind = cardKind(item, kind);
  const search = `${item.title} ${item.description || item.blurb || ""} ${(item.topics || []).join(" ")} ${kind} ${itemKind}`.toLowerCase();
  const difficulty = String(item.difficulty || "sin nivel").toLowerCase();
  const difficultyGroup = difficultyClassName(difficulty);
  const mode = item.testContract?.mode ? `<span>${escapeHtml(`test: ${item.testContract.mode}`)}</span>` : "";
  const combined = (item.combinedTopics || []).length > 0 || (item.primaryTopics || []).length > 1;
  const routeEligible = item.route?.routeEligible === true;
  const typeBadges = [
    `<span>${escapeHtml(combined ? "combinado" : "puro")}</span>`,
    kind === "exercise" ? `<span>${escapeHtml(routeEligible ? "Ruta C" : "categoria")}</span>` : "",
    mode,
  ].filter(Boolean).join("");
  const requirementText = requirementSummary(item, kind);
  const lock = item.unlocked === false ? lockReason(item) : "";
  const questions = kind === "quiz" && Array.isArray(item.questions) ? `<span>${item.questions.length} preguntas</span>` : "";
  const time = kind === "quiz" && (item.timeLimitMinutes || item.difficulty?.expectedMinutes) ? `<span>${escapeHtml(`${item.timeLimitMinutes || item.difficulty?.expectedMinutes} min`)}</span>` : "";
  const actionLabel = item.unlocked === false ? "Bloqueado" : (kind === "quiz" ? "Abrir preview" : "Iniciar");
  return `
    <article class="exerciseCard disabled"
      data-provider="fundamentals"
      data-kind="${escapeHtml(itemKind)}"
      data-status="${escapeHtml(status)}"
      data-status-group="${escapeHtml(statusGroup)}"
      data-difficulty="${escapeHtml(difficultyGroup)}"
      data-topics="${escapeHtml(allTopics)}"
      data-search="${escapeHtml(search)}">
      <div class="icon"><div class="fallbackIcon">${escapeHtml(kindInitials(kind))}</div></div>
      <div class="content">
        <div class="cardHeader">
          <h2>${escapeHtml(item.title || "")}</h2>
          <span class="provider">${escapeHtml(kindLabel(kind))}</span>
        </div>
        <div class="badges">
          <span class="status ${escapeHtml(status)}">${escapeHtml(item.unlocked === false ? "Bloqueado" : statusText(status))}</span>
          <span class="difficulty ${escapeHtml(difficultyGroup)}">${escapeHtml(item.difficulty || "sin nivel")}</span>
          ${typeBadges}
          ${questions}
          ${time}
        </div>
        <p>${escapeHtml(item.description || item.blurb || "")}</p>
        ${requirementText ? `<div class="metaLine">${escapeHtml(requirementText)}</div>` : ""}
        ${lock ? `<div class="lockReason">${escapeHtml(lock)}</div>` : ""}
        <div class="topics">${topics}</div>
      </div>
      <div class="cardActions"><button disabled>${escapeHtml(actionLabel)}</button></div>
    </article>
  `;
}

function cardKind(item, kind) {
  if (kind === "quiz") return item.quizType || "tema";
  if (kind === "project") return item.projectType || item.kind || "project";
  if ((item.combinedTopics || []).length > 0 || (item.primaryTopics || []).length > 1) return "combined";
  return item.kind || "practice";
}

function requirementSummary(item, kind) {
  const required = item.requiredConcepts || item.requiredMastery || [];
  if (kind === "project" && required.length) return `Requiere: ${required.join(", ")}`;
  if (kind === "exercise" && item.testContract?.mode) return `Contrato: ${item.testContract.mode}`;
  return "";
}

function lockReason(item) {
  const unlock = item.unlock || {};
  if ((unlock.requiresExercises || []).length) return `Bloqueado por ejercicios: ${unlock.requiresExercises.join(", ")}`;
  if ((unlock.requiresModules || []).length) return `Bloqueado por modulos: ${unlock.requiresModules.join(", ")}`;
  if (unlock.requiresQuizScore) return `Bloqueado por quiz: ${unlock.requiresQuizScore.quizId || "quiz requerido"}`;
  return "Bloqueado hasta completar requisitos previos.";
}

function kindInitials(kind) {
  return { quiz: "QT", project: "AP", exercise: "FC" }[kind] || "FC";
}

function kindLabel(kind) {
  return { quiz: "Quiz teorico", project: "Asignacion / proyecto", exercise: "Fundamentos C" }[kind] || "Fundamentos C";
}

function difficultyClassName(value) {
  if (value.includes("hard")) return "hard";
  if (value.includes("medium")) return "medium";
  if (value.includes("easy") || value.includes("beginner")) return "easy";
  return "unknown";
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
    not_started: "Disponible",
    current: "Actual",
    locked: "Bloqueado",
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
  if (["locked"].includes(status)) return "locked";
  return "available";
}

function providerInitials(exercise) {
  if (exercise.provider === "alejandro") return "AL";
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
    .rootGrid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; margin-top: 18px; }
    .rootGrid.compactRoots { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .rootCard { display: grid; grid-template-columns: 70px minmax(0, 1fr); gap: 16px; align-items: center; border: 1px solid var(--border); background: var(--card); border-radius: 8px; padding: 18px; box-shadow: var(--shadow); cursor: pointer; min-height: 112px; }
    .rootCard:hover, .rootCard:focus-visible { border-color: var(--accent); outline: none; background: var(--cardHover); }
    .subhead { display: flex; align-items: center; gap: 12px; margin: 8px 0 14px; }
    .routePath { display: grid; gap: 14px; }
    .routeRoadmap { padding: 4px 0 18px; }
    .routeTabs { display: flex; gap: 8px; overflow-x: auto; padding: 2px 0 10px; }
    .routeTab { flex: 0 0 auto; border-color: color-mix(in srgb, var(--route-accent) 50%, var(--border)); background: color-mix(in srgb, var(--route-accent) 11%, var(--panel)); color: var(--fg); }
    .routeTab.active { border-color: var(--route-accent); box-shadow: 0 0 20px var(--route-glow); }
    .routeModuleCard {
      position: relative;
      overflow: hidden;
      border: 1px solid color-mix(in srgb, var(--route-accent) 35%, var(--border));
      border-radius: 12px;
      background:
        linear-gradient(180deg, rgba(255,255,255,.035), transparent 34%),
        radial-gradient(circle at 74% 40%, var(--route-glow), transparent 34%),
        linear-gradient(135deg, color-mix(in srgb, var(--route-accent) 10%, #06131a), #07131c 62%, #041017);
      box-shadow: 0 22px 56px rgba(0,0,0,.28), inset 0 0 0 1px rgba(255,255,255,.035);
      min-height: 540px;
    }
    .routeModuleCard::before {
      content: "";
      position: absolute;
      inset: 0;
      background-image:
        linear-gradient(rgba(255,255,255,.035) 1px, transparent 1px),
        linear-gradient(90deg, rgba(255,255,255,.035) 1px, transparent 1px);
      background-size: 32px 32px;
      opacity: .28;
      pointer-events: none;
    }
    .routeHero {
      position: relative;
      z-index: 1;
      display: flex;
      justify-content: space-between;
      gap: 14px;
      align-items: center;
      margin: 18px;
      padding: 18px 20px;
      border-radius: 10px;
      background: linear-gradient(135deg, var(--route-accent), var(--route-accent-2));
      color: white;
      box-shadow: 0 12px 34px var(--route-glow);
    }
    .routeHero h3 { margin: 3px 0 0; color: white; font-size: 24px; }
    .routeHero p { margin: 5px 0 0; color: rgba(255,255,255,.78); font-size: 13px; }
    .routeEyebrow { display: block; font-size: 11px; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; opacity: .86; }
    .routeGuideButton { border-color: rgba(255,255,255,.36); background: rgba(0,0,0,.16); color: white; }
    .routeProgress {
      position: relative;
      z-index: 1;
      display: grid;
      grid-template-columns: auto minmax(120px, 1fr) auto;
      gap: 12px;
      align-items: center;
      margin: -8px 18px 18px;
      padding: 10px 14px;
      border: 1px solid rgba(255,255,255,.08);
      border-radius: 10px;
      background: rgba(0,0,0,.18);
      color: var(--muted);
      font-size: 12px;
    }
    .routeSegments { display: flex; gap: 5px; min-width: 0; }
    .routeSegments span { height: 7px; flex: 1; border-radius: 999px; background: rgba(255,255,255,.09); }
    .routeSegments span.done, .routeSegments span.current { background: var(--route-accent); box-shadow: 0 0 14px var(--route-glow); }
    .routeBody { position: relative; z-index: 1; display: grid; grid-template-columns: 190px minmax(180px, 1fr) 210px; gap: 18px; align-items: stretch; padding: 0 18px 18px; }
    .snakeWrap { position: relative; min-height: 330px; }
    .snakeLine { position: absolute; inset: 0; width: 100%; height: 100%; }
    .snakeLine path { fill: none; stroke: color-mix(in srgb, var(--route-accent) 60%, #ffffff); stroke-width: 4; stroke-linecap: round; filter: drop-shadow(0 0 8px var(--route-glow)); opacity: .68; }
    .snakeNodes { position: relative; min-height: inherit; }
    .snakeNode {
      position: absolute;
      width: 44px;
      height: 44px;
      border-radius: 999px;
      display: grid;
      place-items: center;
      padding: 0;
      font-weight: 900;
      border: 2px solid rgba(255,255,255,.22);
      background: rgba(17,32,43,.94);
      color: var(--muted);
      box-shadow: 0 0 0 6px rgba(255,255,255,.035);
    }
    .snakeNode.left { left: 28px; }
    .snakeNode.right { right: 28px; }
    .snakeNode.completed { background: var(--route-accent); color: white; border-color: color-mix(in srgb, white 45%, var(--route-accent)); box-shadow: 0 0 24px var(--route-glow); }
    .snakeNode.current { background: #f7ffff; color: #08202a; border-color: var(--route-accent); box-shadow: 0 0 0 7px color-mix(in srgb, var(--route-accent) 24%, transparent), 0 0 32px var(--route-accent); animation: routePulse 1.7s ease-in-out infinite; }
    .snakeNode.available { color: white; border-color: var(--route-accent); background: color-mix(in srgb, var(--route-accent) 32%, #13232c); }
    .snakeNode.locked { color: #9cabb6; background: #1b2a34; border-color: rgba(255,255,255,.08); box-shadow: none; opacity: .82; }
    .snakeNode.selected { outline: 2px solid white; outline-offset: 3px; }
    .snakeNode.challenge { clip-path: polygon(50% 0%, 62% 30%, 96% 35%, 70% 57%, 79% 91%, 50% 72%, 21% 91%, 30% 57%, 4% 35%, 38% 30%); border-radius: 0; }
    .snakeNode.current::after {
      content: "Sigue aqui";
      position: absolute;
      left: 50px;
      top: 50%;
      transform: translateY(-50%);
      border: 1px solid var(--route-accent);
      border-radius: 999px;
      padding: 3px 8px;
      color: var(--route-accent);
      background: rgba(0,0,0,.36);
      font-size: 10px;
      white-space: nowrap;
      font-weight: 800;
    }
    @keyframes routePulse { 0%, 100% { transform: scale(1); } 50% { transform: scale(1.08); } }
    .routeNodeList { display: flex; flex-direction: column; justify-content: center; gap: 9px; min-width: 0; }
    .routeListItem {
      display: grid;
      grid-template-columns: 28px minmax(0, 1fr);
      gap: 8px;
      align-items: center;
      text-align: left;
      white-space: normal;
      border-color: rgba(255,255,255,.08);
      background: rgba(255,255,255,.035);
      color: var(--fg);
    }
    .routeListItem span { grid-row: 1 / span 2; width: 26px; height: 26px; display: grid; place-items: center; border-radius: 999px; background: color-mix(in srgb, var(--route-accent) 20%, #0a1a23); color: var(--route-accent); font-weight: 800; }
    .routeListItem strong, .routeListItem small { overflow: hidden; text-overflow: ellipsis; }
    .routeListItem small { color: var(--muted); font-size: 11px; }
    .routeListItem.active { border-color: var(--route-accent); background: color-mix(in srgb, var(--route-accent) 13%, rgba(255,255,255,.04)); }
    .routeVisual { display: flex; flex-direction: column; justify-content: center; gap: 14px; min-width: 0; }
    .routeIllustration { width: 100%; min-height: 150px; filter: drop-shadow(0 0 18px var(--route-glow)); }
    .routeDetail { border: 1px solid rgba(255,255,255,.09); border-radius: 10px; background: rgba(0,0,0,.18); padding: 14px; }
    .routeDetailState { color: var(--route-accent); font-size: 12px; font-weight: 800; }
    .routeDetail h4 { margin: 6px 0 0; font-size: 16px; }
    .routeDetail p { font-size: 13px; margin-top: 8px; }
    .routeDetailMeta { color: var(--muted); font-size: 12px; margin: 10px 0; }
    .routeDetail button { border-color: var(--route-accent); color: var(--fg); background: color-mix(in srgb, var(--route-accent) 18%, transparent); }
    .routeFooter {
      position: relative;
      z-index: 1;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
      margin: 0 18px 18px;
      padding: 12px 14px;
      border: 1px solid rgba(255,255,255,.08);
      border-radius: 10px;
      background: rgba(0,0,0,.18);
      color: var(--muted);
      font-size: 13px;
    }
    .routeFooter button { border-color: var(--route-accent); color: var(--route-accent); background: rgba(255,255,255,.04); }
    .routeLegend { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; justify-content: center; }
    .routeLegend span { display: inline-flex; align-items: center; gap: 5px; font-size: 11px; color: var(--muted); }
    .legendNode { width: 20px; height: 20px; display: grid; place-items: center; border-radius: 999px; font-style: normal; font-size: 10px; font-weight: 900; border: 1px solid rgba(255,255,255,.16); }
    .legendNode.completed { background: var(--route-accent); color: white; }
    .legendNode.current { background: white; color: #08202a; border-color: var(--route-accent); box-shadow: 0 0 12px var(--route-glow); }
    .legendNode.locked { background: #1b2a34; color: #9cabb6; }
    .legendNode.challenge { background: color-mix(in srgb, var(--route-accent) 28%, #13232c); color: white; clip-path: polygon(50% 0%, 62% 30%, 96% 35%, 70% 57%, 79% 91%, 50% 72%, 21% 91%, 30% 57%, 4% 35%, 38% 30%); border-radius: 0; }
    .routeModule { border: 1px solid var(--border); background: var(--card); border-radius: 8px; padding: 16px; }
    .routeModule.locked, .routeNode.locked { opacity: 0.55; }
    .routeModuleHead { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
    .routeModuleHead span { color: var(--muted); font-size: 12px; }
    .routeNodes { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 10px; }
    .routeNode { border: 1px solid var(--border); background: var(--pill); border-radius: 8px; min-height: 74px; padding: 12px; display: flex; flex-direction: column; justify-content: center; gap: 6px; }
    .routeNode span { color: var(--muted); font-size: 12px; }
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
    .metaLine, .lockReason { color: var(--soft); font-size: 12px; line-height: 1.35; margin-top: 8px; }
    .lockReason { color: var(--warn); }
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
      .rootGrid, .rootGrid.compactRoots { grid-template-columns: 1fr; }
      .rootCard { grid-template-columns: 58px minmax(0, 1fr); }
      .routeHero, .routeFooter, .routeProgress { margin-left: 12px; margin-right: 12px; }
      .routeBody { grid-template-columns: 1fr; padding-left: 12px; padding-right: 12px; }
      .snakeWrap { min-height: 300px; max-width: 240px; margin: 0 auto; width: 100%; }
      .routeNodeList { order: 2; }
      .routeVisual { order: 3; }
      .routeProgress { grid-template-columns: 1fr; }
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
    theme: localStorage.getItem('estudio.theme') || 'light',
    logic: localStorage.getItem('estudio.logic') || 'or',
    view: localStorage.getItem('estudio.view') || 'home'
  };
  setTheme(state.theme);
  setView(state.view);
  setActiveButtons();
  applyFilters();

  document.addEventListener('click', (event) => {
    const button = event.target.closest('button');
    if (button?.dataset.view) {
      setView(button.dataset.view);
      return;
    }
    if (button?.classList.contains('filterToggle')) {
      const section = button.closest('.viewSection') || document;
      const panel = section.querySelector('.filterPanel');
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
    if (button?.classList.contains('routeTab')) {
      showRouteModule(button.dataset.routeTarget);
      return;
    }
    if (button?.classList.contains('snakeNode') || button?.classList.contains('routeListItem')) {
      selectRouteNode(button);
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
    const rootCard = event.target.closest('.rootCard');
    if (rootCard?.dataset.view) setView(rootCard.dataset.view);
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
  document.querySelectorAll('.searchBox').forEach((input) => input.addEventListener('input', applyFilters));

  function postCardCommand(card) {
    const command = card.dataset.command;
    if (!command) return;
    vscode.postMessage({ command, provider: card.dataset.providerAction, slug: card.dataset.slug, folder: card.dataset.folder });
  }
  function setTheme(theme) {
    document.documentElement.dataset.theme = ['light', 'dark', 'system'].includes(theme) ? theme : 'light';
  }
  function setView(view) {
    state.view = view || 'home';
    const fundamentalsView = state.view.startsWith('fundamentals');
    if (fundamentalsView) state.provider = 'fundamentals';
    if (state.view === 'exercism' && state.provider === 'fundamentals') state.provider = 'exercism';
    localStorage.setItem('estudio.view', state.view);
    document.querySelectorAll('.viewSection').forEach((section) => {
      section.classList.toggle('hidden', section.dataset.viewSection !== state.view);
    });
    setActiveButtons();
    applyFilters();
  }
  function setActiveButtons() {
    document.querySelectorAll('.providerFilter').forEach((button) => button.classList.toggle('active', button.dataset.provider === state.provider));
    document.querySelectorAll('.statusFilter').forEach((button) => button.classList.toggle('active', button.dataset.status === state.status));
    document.querySelectorAll('.themeButton').forEach((button) => button.classList.toggle('active', button.dataset.theme === state.theme));
    document.querySelectorAll('.logicButton').forEach((button) => button.classList.toggle('active', button.dataset.logic === state.logic));
  }
  function showRouteModule(moduleId) {
    if (!moduleId) return;
    document.querySelectorAll('.routeTab').forEach((button) => button.classList.toggle('active', button.dataset.routeTarget === moduleId));
    document.querySelectorAll('.routeModuleCard').forEach((card) => card.classList.toggle('hidden', card.dataset.routeModule !== moduleId));
  }
  function selectRouteNode(button) {
    const moduleCard = button.closest('.routeModuleCard');
    const ref = button.dataset.routeNodeRef;
    if (!moduleCard || !ref) return;
    const source = moduleCard.querySelector(\`.snakeNode[data-route-node-ref="\${cssEscape(ref)}"]\`);
    if (!source) return;
    moduleCard.querySelectorAll('.routeListItem').forEach((item) => item.classList.toggle('active', item.dataset.routeNodeRef === ref));
    moduleCard.querySelectorAll('.snakeNode').forEach((item) => item.classList.toggle('selected', item.dataset.routeNodeRef === ref));
    const detail = moduleCard.querySelector('[data-route-detail]');
    if (!detail) return;
    const locked = source.dataset.routeState === 'locked';
    detail.innerHTML = [
      \`<span class="routeDetailState">\${locked ? 'Bloqueado' : (source.dataset.routeState === 'current' ? 'Actual' : 'Disponible')}</span>\`,
      \`<h4>\${escapeText(source.dataset.routeTitle || 'Nodo')}</h4>\`,
      \`<p>\${escapeText(locked ? (source.dataset.routeLock || 'Completa los pasos anteriores.') : (source.dataset.routeDescription || 'Listo para continuar cuando quieras.'))}</p>\`,
      \`<div class="routeDetailMeta">\${escapeText(source.dataset.routeLabel || '')}</div>\`,
      \`<button disabled>\${locked ? 'Bloqueado' : 'Iniciar'}</button>\`
    ].join('');
  }
  function cssEscape(value) {
    if (window.CSS && typeof window.CSS.escape === 'function') return window.CSS.escape(value);
    return String(value).replace(/["\\\\]/g, '\\\\$&');
  }
  function escapeText(value) {
    return String(value || '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
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
    const section = document.querySelector(\`.viewSection[data-view-section="\${state.view}"]\`) || document;
    const query = (section.querySelector('.searchBox')?.value || '').trim().toLowerCase();
    const difficulties = Array.from(section.querySelectorAll('input[data-difficulty]:checked')).map((item) => item.dataset.difficulty).filter(Boolean);
    const kinds = Array.from(section.querySelectorAll('input[data-kind]:checked')).map((item) => item.dataset.kind).filter(Boolean);
    const includeTopics = Array.from(section.querySelectorAll('.topicToggle[data-topic-state="include"]')).map((item) => item.dataset.topic).filter(Boolean).map((x) => x.toLowerCase());
    const excludeTopics = Array.from(section.querySelectorAll('.topicToggle[data-topic-state="exclude"]')).map((item) => item.dataset.topic).filter(Boolean).map((x) => x.toLowerCase());
    let visible = 0;
    const fundamentalsView = state.view.startsWith('fundamentals');
    section.querySelectorAll('.exerciseCard').forEach((card) => {
      const topics = (card.dataset.topics || '').split('|').filter(Boolean);
      const providerOk = fundamentalsView || state.provider === 'all' || card.dataset.provider === state.provider;
      const statusOk = state.status === 'all' || card.dataset.statusGroup === state.status;
      const queryOk = !query || (card.dataset.search || '').includes(query);
      const difficultyOk = difficulties.length === 0 || difficulties.includes(card.dataset.difficulty || '');
      const kindOk = kinds.length === 0 || kinds.includes(card.dataset.kind || '');
      const includeOk = includeTopics.length === 0 || (state.logic === 'and'
        ? includeTopics.every((topic) => topics.includes(topic))
        : includeTopics.some((topic) => topics.includes(topic)));
      const excludeOk = excludeTopics.length === 0 || !excludeTopics.some((topic) => topics.includes(topic));
      const show = providerOk && statusOk && queryOk && difficultyOk && kindOk && includeOk && excludeOk;
      card.classList.toggle('hidden', !show);
      if (show) visible += 1;
    });
    section.querySelector('.empty')?.classList.toggle('hidden', visible !== 0);
    updateCounts(section);
  }
  function updateCounts(section = document) {
    const counts = { all: 0, completed: 0, in_progress: 0, available: 0 };
    const fundamentalsView = state.view.startsWith('fundamentals');
    section.querySelectorAll('.exerciseCard').forEach((card) => {
      const providerOk = fundamentalsView || state.provider === 'all' || card.dataset.provider === state.provider;
      if (!providerOk) return;
      counts.all += 1;
      const group = card.dataset.statusGroup || 'available';
      if (Object.prototype.hasOwnProperty.call(counts, group)) counts[group] += 1;
    });
    Object.entries(counts).forEach(([key, value]) => {
      section.querySelectorAll(\`[data-count="\${key}"]\`).forEach((item) => { item.textContent = value; });
    });
  }
</script>
</body>
</html>`;
}

module.exports = { activate, deactivate };
