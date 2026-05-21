import React, { useCallback, useEffect, useMemo, useState } from "react";
import { BackendAction, isBridgeAvailable, requestBackend } from "./bridgeClient.js";
import {
  finalIssues,
  finalReadiness,
  finalText,
  finalTitle,
} from "./finalState.js";
import {
  modeForWorkflow,
  statusClass,
  statusLabel,
  summaryIsReady,
  toolsFromSnapshot,
} from "./stateMapping.js";
import {
  normalizeAliasInput,
  recommendedWorkspacePath,
  resolveWorkspaceSelection,
} from "./workspaceState.js";

import nodejsIcon from "./assets/tools/nodejs.svg";
import pythonIcon from "./assets/tools/python.svg";
import gitIcon from "./assets/tools/git.svg";
import githubCliIcon from "./assets/tools/github-cli.svg";
import githubIcon from "./assets/tools/github.svg";
import vscodeIcon from "./assets/tools/vscode.svg";
import msys2Icon from "./assets/tools/msys2.svg";
import gccIcon from "./assets/tools/gcc.svg";
import makeIcon from "./assets/tools/make.svg";
import exercismIcon from "./assets/tools/exercism.svg";
import workspaceIcon from "./assets/tools/workspace.svg";

const SCREEN_ORDER = ["welcome", "workflow", "scan", "components", "accounts", "execute"];

const WORKFLOWS = [
  { id: "setup", title: "Configurar por primera vez", subtitle: "Deja el entorno completo listo para estudiar C.", badge: "Recomendado", icon: "spark" },
  { id: "update", title: "Actualizar instalación", subtitle: "Migra, actualiza extensión y repara faltantes sin tocar tu trabajo.", badge: "Nuevo", icon: "refresh" },
  { id: "repair", title: "Reparar instalación", subtitle: "Corrige herramientas, cuentas o la compilación con F9.", badge: "Seguro", icon: "repair" },
  { id: "reinstall", title: "Reinstalar entorno", subtitle: "Reconstruye herramientas sin borrar ejercicios ni logs.", badge: "Avanzado", icon: "refresh" },
  { id: "accounts", title: "Cuentas y ejercicios", subtitle: "Cambia GitHub o vuelve a conectar Exercism.", badge: "Rápido", icon: "account" },
  { id: "uninstall", title: "Desinstalar o limpiar", subtitle: "Quita lo gestionado y conserva tu trabajo.", badge: "Cuidado", icon: "trash" },
];

const COMPONENTS_BY_WORKFLOW = {
  setup: [
    { id: "base", title: "Herramientas base", detail: "Node.js, Python, Git y GitHub CLI", type: "required" },
    { id: "editor", title: "Editor", detail: "VS Code configurado para el workspace", type: "required" },
    { id: "c-toolchain", title: "Compilador C", detail: "MSYS2 UCRT64, GCC y Make", type: "required" },
    { id: "github", title: "GitHub", detail: "Cuenta, fork y remotos del workspace", type: "account" },
    { id: "exercism", title: "Exercism", detail: "CLI, token y track de C", type: "account" },
    { id: "workspace", title: "Workspace", detail: "Ejercicios, README, logs y prueba F9", type: "required" },
  ],
  update: [
    { id: "manifest", title: "Leer instalación actual", detail: "Detectar versión, manifest y componentes gestionados", type: "required" },
    { id: "tools", title: "Actualizar y reparar", detail: "Instalar faltantes, obsoletos o rotos", type: "required" },
    { id: "editor", title: "Actualizar extensión", detail: "VS Code, panel Exercism y settings del workspace", type: "required" },
    { id: "keep-data", title: "Conservar trabajo", detail: "Ejercicios, usuario, logs y API keys locales", type: "safety" },
    { id: "verify", title: "Revalidar entorno", detail: "Workspace, cuentas, Exercism y F9", type: "required" },
  ],
  repair: [
    { id: "tools", title: "Reparar herramientas", detail: "Dependencias faltantes, rotas o desactualizadas", type: "required" },
    { id: "editor", title: "Revalidar VS Code", detail: "Comando code, perfil y apertura del workspace", type: "required" },
    { id: "c-toolchain", title: "Reparar compilador C", detail: "MSYS2 UCRT64, GCC, Make y PATH", type: "required" },
    { id: "github", title: "Revalidar GitHub", detail: "Cuenta activa, fork y remotos", type: "account" },
    { id: "verify", title: "Validar F9", detail: "Smoke test sin commits automáticos", type: "required" },
  ],
  reinstall: [
    { id: "manifest", title: "Leer instalación actual", detail: "Detectar herramientas gestionadas", type: "required" },
    { id: "tools", title: "Reinstalar herramientas", detail: "Base, editor, C toolchain y Exercism", type: "required" },
    { id: "keep-data", title: "Conservar datos", detail: "Ejercicios, logs y usuario/", type: "safety" },
    { id: "github", title: "Reconfigurar GitHub", detail: "Cuenta, fork y remotos", type: "account" },
    { id: "exercism", title: "Reconfigurar Exercism", detail: "Token y track de C", type: "account" },
    { id: "verify", title: "Validar entorno", detail: "Workspace y F9", type: "required" },
  ],
  accounts: [
    { id: "github", title: "GitHub", detail: "Cambiar o confirmar cuenta", type: "account" },
    { id: "exercism", title: "Exercism", detail: "Configurar token y track de C", type: "account" },
    { id: "verify", title: "Validar importación", detail: "Comprobar ejercicios y workspace", type: "required" },
  ],
  uninstall: [
    { id: "managed", title: "Quitar elementos gestionados", detail: "Solo lo atribuido al configurador", type: "required" },
    { id: "config", title: "Eliminar configuración local", detail: "Preferencias y manifiesto del configurador", type: "required" },
    { id: "keep", title: "Conservar trabajo del estudiante", detail: "Ejercicios, logs, workspace y usuario/", type: "safety" },
    { id: "report", title: "Generar reporte", detail: "Resumen final de limpieza", type: "required" },
  ],
};

const TOOL_ICONS = {
  nodejs: nodejsIcon,
  python: pythonIcon,
  git: gitIcon,
  githubcli: githubCliIcon,
  github: githubIcon,
  vscode: vscodeIcon,
  msys2: msys2Icon,
  gcc: gccIcon,
  make: makeIcon,
  exercism: exercismIcon,
  workspace: workspaceIcon,
};

const Icons = {
  Spark: ({ className = "h-5 w-5" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><path strokeLinecap="round" strokeLinejoin="round" d="M12 3l1.8 4.8L18.8 9.6l-5 1.8L12 16l-1.8-4.6-5-1.8 5-1.8L12 3Z" /><path strokeLinecap="round" strokeLinejoin="round" d="M19 15l.8 2.1L22 18l-2.2.9L19 21l-.8-2.1L16 18l2.2-.9L19 15Z" /></svg>,
  Check: ({ className = "h-5 w-5" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.4"><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" /></svg>,
  Arrow: ({ className = "h-4 w-4" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.2"><path strokeLinecap="round" strokeLinejoin="round" d="M5 12h14m-6-6 6 6-6 6" /></svg>,
  Back: ({ className = "h-4 w-4" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.2"><path strokeLinecap="round" strokeLinejoin="round" d="M19 12H5m6-6-6 6 6 6" /></svg>,
  Loader: ({ className = "h-5 w-5" }) => <svg className={`${className} spin`} fill="none" viewBox="0 0 24 24"><circle className="opacity-20" cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="3" /><path className="opacity-90" fill="currentColor" d="M21 12a9 9 0 0 0-9-9v3a6 6 0 0 1 6 6h3Z" /></svg>,
  Terminal: ({ className = "h-4 w-4" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><rect x="3" y="5" width="18" height="14" rx="2" /><path strokeLinecap="round" strokeLinejoin="round" d="m7 10 3 2-3 2M13 15h4" /></svg>,
  Repair: ({ className = "h-5 w-5" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><path strokeLinecap="round" strokeLinejoin="round" d="M14.7 6.3a4 4 0 0 0-5.4 5.4L4 17l3 3 5.3-5.3a4 4 0 0 0 5.4-5.4L15 12l-3-3 2.7-2.7Z" /></svg>,
  Refresh: ({ className = "h-5 w-5" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><path strokeLinecap="round" strokeLinejoin="round" d="M3 12a9 9 0 1 0 3-6.7" /><path strokeLinecap="round" strokeLinejoin="round" d="M3 3v6h6" /></svg>,
  Account: ({ className = "h-5 w-5" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><circle cx="12" cy="8" r="4" /><path strokeLinecap="round" strokeLinejoin="round" d="M4 21a8 8 0 0 1 16 0" /></svg>,
  Trash: ({ className = "h-5 w-5" }) => <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8"><path strokeLinecap="round" strokeLinejoin="round" d="M3 6h18M8 6V4h8v2m-1 4v8M9 10v8m-3-12 1 16h10l1-16" /></svg>,
};

function workflowIcon(id) {
  if (id === "repair") return Icons.Repair;
  if (id === "reinstall" || id === "update") return Icons.Refresh;
  if (id === "accounts") return Icons.Account;
  if (id === "uninstall") return Icons.Trash;
  return Icons.Spark;
}

function workflowNeedsGithub(workflow) {
  return ["setup", "update", "repair", "reinstall", "accounts"].includes(workflow);
}

function workflowNeedsExercism(workflow) {
  return ["setup", "update", "repair", "reinstall", "accounts"].includes(workflow);
}

function bridgeEventName(type) {
  return String(type || "").toLowerCase();
}

function messageFromError(error) {
  return error?.description || error?.message || error?.title || String(error || "Error desconocido");
}

function Button({ children, onClick, variant = "primary", disabled, icon = true, type = "button" }) {
  const cls = variant === "primary" ? "primary-button" : variant === "danger" ? "danger-button" : "secondary-button";
  return <button type={type} onClick={onClick} disabled={disabled} className={cls}>{children}{icon ? <Icons.Arrow /> : null}</button>;
}

function ToolLogo({ icon }) {
  const src = TOOL_ICONS[icon] || workspaceIcon;
  return <img className="tool-logo" src={src} alt="" aria-hidden="true" />;
}

function HeaderBlock({ eyebrow, title, text }) {
  return <div><p className="kicker">{eyebrow}</p><h1 className="title">{title}</h1>{text ? <p className="subtitle">{text}</p> : null}</div>;
}

function SetupPanel({ children, wide = false }) {
  return <section className={`panel enter-soft ${wide ? "wide" : "normal"}`}>{children}</section>;
}

function AppShell({ children, stage, canGoBack, onBack, consoleOpen, setConsoleOpen, logs }) {
  return <div className="estudio-ui">
    <div className="dark-bloom" />
    <header className="app-header">
      <div className="header-left">
        {canGoBack ? <button onClick={onBack} className="back-button" aria-label="Volver"><Icons.Back /></button> : null}
        <div className="logo-box">S</div>
        <div><p className="brand-title">Estudio Socrático</p><p className="brand-subtitle">Configurador</p></div>
      </div>
      <div className="stage-pill">{stage}</div>
    </header>
    <main className="main">{children}</main>
    <footer className="app-footer"><span>WinUI · WebView2 · instalación segura</span><button className="footer-button" onClick={() => setConsoleOpen(!consoleOpen)}><Icons.Terminal /> Diagnóstico técnico</button></footer>
    {consoleOpen ? <TechConsole logs={logs} onClose={() => setConsoleOpen(false)} /> : null}
  </div>;
}

function Welcome({ onNext }) {
  return <SetupPanel>
    <div className="hero-mark"><Icons.Spark className="h-7 w-7" /></div>
    <HeaderBlock eyebrow="Configuración" title="Preparemos Estudio Socrático" text="Elige qué necesitas hacer. El configurador instalará y reparará las herramientas necesarias según ese caso de uso." />
    <div className="center-actions"><Button onClick={onNext}>Continuar</Button></div>
  </SetupPanel>;
}

function WorkflowScreen({ selectedWorkflow, setSelectedWorkflow, onNext }) {
  return <SetupPanel wide>
    <HeaderBlock eyebrow="Caso de uso" title="¿Qué necesitas hacer?" text="El instalador ajustará la preparación según tu situación. Las herramientas necesarias no se tratan como opcionales." />
    <div className="grid-workflows">{WORKFLOWS.map(w => <WorkflowCard key={w.id} workflow={w} selected={selectedWorkflow === w.id} onClick={() => setSelectedWorkflow(w.id)} />)}</div>
    <div className="center-actions"><Button onClick={onNext} disabled={!selectedWorkflow}>Continuar</Button></div>
  </SetupPanel>;
}

function WorkflowCard({ workflow, selected, onClick }) {
  const Icon = workflowIcon(workflow.id);
  return <button className={`workflow-card ${selected ? "selected" : ""}`} onClick={onClick}>
    <div className="workflow-card-head">
      <div className="icon-tile"><Icon /></div><span className="badge">{workflow.badge}</span>
    </div>
    <h3>{workflow.title}</h3>
    <p>{workflow.subtitle}</p>
  </button>;
}

function ScanScreen({ selectedWorkflow, tools, loading, backendAvailable, onRefresh, onNext }) {
  const visibleTools = selectedWorkflow === "uninstall"
    ? tools
    : tools;
  return <SetupPanel wide>
    <HeaderBlock eyebrow="Revisión" title="Herramientas detectadas" text="El instalador revisa cada pieza que maneja Estudio Socrático y muestra su estado real antes de continuar." />
    <div className="scan-toolbar">
      <span className={`status-badge ${backendAvailable ? "status-ready" : "status-error"}`}>{backendAvailable ? "Bridge conectado" : "Bridge no disponible"}</span>
      <Button variant="secondary" onClick={onRefresh} disabled={loading} icon={false}>{loading ? <Icons.Loader className="h-4 w-4" /> : null} Revisar ahora</Button>
    </div>
    <div className="tools-grid">{visibleTools.map(tool => <ToolStatusCard key={tool.id} tool={tool} />)}</div>
    <div className="legend-row">
      {["ready", "missing", "repair", "action", "installing", "error"].map(s => <span key={s} className={`status-badge ${statusClass(s)}`}>{statusLabel(s)}</span>)}
    </div>
    <div className="center-actions"><Button onClick={onNext}>Ver preparación</Button></div>
  </SetupPanel>;
}

function ToolStatusCard({ tool }) {
  const hasLongDetail = String(tool.detail || "").length > 72;
  return <div className="tool-card">
    <div className="tool-card-head">
      <ToolLogo icon={tool.icon} /><span className={`status-badge ${statusClass(tool.status)}`}>{tool.label}</span>
    </div>
    <p className="tool-name" title={tool.name}>{tool.name}</p>
    {hasLongDetail
      ? <details className="tool-detail" title={tool.detail}><summary>Ver detalle</summary><p>{tool.detail}</p></details>
      : <p className="tool-group" title={tool.detail || tool.group}>{tool.detail || tool.group}</p>}
  </div>;
}

function ComponentsScreen({ selectedWorkflow, onNext }) {
  const components = COMPONENTS_BY_WORKFLOW[selectedWorkflow] || [];
  const workflow = WORKFLOWS.find(w => w.id === selectedWorkflow);
  return <SetupPanel wide>
    <HeaderBlock eyebrow="Preparación" title={selectedWorkflow === "uninstall" ? "Desinstalar Estudio Socrático" : "Esto quedará preparado"} text={selectedWorkflow === "uninstall" ? "Se quitarán herramientas y configuración gestionadas por el instalador. Tu trabajo de estudiante se conservará." : `${workflow?.title || "Flujo seleccionado"}. Estos pasos son parte del funcionamiento esperado de Estudio Socrático.`} />
    <div className="components-list">{components.map(c => <ComponentRow key={c.id} component={c} />)}</div>
    <div className="center-actions"><Button onClick={onNext}>Continuar</Button></div>
  </SetupPanel>;
}

function ComponentRow({ component }) {
  const cls = component.type === "account" ? "status-action" : component.type === "safety" ? "status-ready" : "";
  const label = component.type === "account" ? "Requiere cuenta" : component.type === "safety" ? "Protegido" : "Necesario";
  return <div className="component-row">
    <div className={`status-badge ${cls}`}><Icons.Check className="h-4 w-4" /></div>
    <div className="component-text"><p>{component.title}</p><span>{component.detail}</span></div>
    <span className={`status-badge ${cls}`}>{label}</span>
  </div>;
}

function UninstallPreview({ report }) {
  if (!report) return null;

  const itemGroups = [
    { title: "Esto se eliminaría", items: report.wouldRemovePaths || report.removedPaths || [], empty: "No hay rutas gestionadas listas para eliminar." },
    { title: "Esto se conservará", items: report.keptPaths || [], empty: "No se detectaron rutas protegidas." },
    { title: "Esto no se tocará porque no es seguro", items: report.skippedPaths || [], empty: "No hay rutas inseguras o ambiguas." },
  ];

  return <div className="uninstall-preview">
    <div className="preview-head">
      <span className="status-badge status-action">{report.dryRun ? "Dry-run" : "Aplicado"}</span>
      <span>{report.message || "Reporte de limpieza generado."}</span>
    </div>
    <div className="preview-grid">
      {itemGroups.map(group => <div className="preview-column" key={group.title}>
        <h3>{group.title}</h3>
        {group.items.length > 0
          ? <ul>{group.items.slice(0, 8).map(item => <li key={`${group.title}-${item}`}>{item}</li>)}</ul>
          : <p>{group.empty}</p>}
      </div>)}
    </div>
  </div>;
}

function AccountsScreen({
  selectedWorkflow,
  snapshot,
  github,
  exercism,
  exercismToken,
  setExercismToken,
  localAlias,
  onAliasInput,
  onChangeAlias,
  selectedWorkspacePath,
  recommendedPath,
  workspaceCustomized,
  onChangeWorkspace,
  onRevealWorkspace,
  onConfigureGithub,
  onChangeGithub,
  onConfigureExercism,
  onReinstallVSCodeExtension,
  onOpenVSCode,
  onOpenExercisePanel,
  onOpenApiKeyConfig,
  onRevealApiKeyConfig,
  onNext,
  busyAction,
}) {
  const needsGithub = workflowNeedsGithub(selectedWorkflow);
  const needsExercism = workflowNeedsExercism(selectedWorkflow);
  const githubReady = github?.configured === true;
  const exercismReady = exercism?.configured === true;
  const workspaceReady = Boolean(String(selectedWorkspacePath || "").trim());
  const workspaceConfigured = snapshot?.workspaceValid === true;
  const extensionState = snapshot?.vsCodeExtension || {};
  const extensionConfig = snapshot?.extensionApiKeyConfig || {};
  const extensionStatus = extensionState.status || "needsUserAction";
  const apiKeyStatus = extensionConfig.status || "needsUserAction";
  const canContinue = selectedWorkflow === "uninstall" ||
    workspaceReady &&
    (!needsGithub || githubReady) &&
    (!needsExercism || exercismReady || exercismToken.trim().length > 0);

  return <SetupPanel>
    <HeaderBlock eyebrow="Cuentas y preparación" title="Revisa identidad, workspace y extensión" text="Aquí quedan visibles el alias local, la carpeta recomendada, las cuentas, la extensión de VS Code y el archivo local de API Key." />
    <div className="account-stack">
      <div className="account-box">
        <div className="account-row">
          <div className="account-title">
            <div className="icon-tile" style={{
              width: "32px",
              height: "32px",
              borderRadius: "8px",
              background: "#111",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              border: "1px solid rgba(255, 255, 255, 0.08)",
              flexShrink: 0
            }}>
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="#60a5fa" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
            </div>
            <div>
              <p>Alias local (Identidad)</p>
              <span>Se usará para firmar tus logs e intentos.</span>
            </div>
          </div>
          <div className="account-buttons">
            <Button variant="secondary" onClick={onChangeAlias} icon={false}>Cambiar alias</Button>
          </div>
        </div>
        <div className="input-row" style={{ marginTop: "12px" }}>
          <input
            className="token-input"
            value={localAlias}
            onChange={e => onAliasInput(e.target.value)}
            type="text"
            placeholder={github?.userName || "ej. juan-perez"}
          />
        </div>
      </div>

      <div className="account-box">
        <div className="account-row account-row-top">
          <div className="account-title"><ToolLogo icon="workspace" /><div><p>Workspace recomendado</p><span>{recommendedPath || "Sin ruta recomendada todavía."}</span></div></div>
          <div className="account-buttons">
            <span className={`status-badge ${workspaceReady ? "status-ready" : "status-action"}`}>{workspaceReady ? "Listo" : "Requiere accion"}</span>
            <Button variant="secondary" onClick={onChangeWorkspace} icon={false}>Cambiar carpeta</Button>
            <Button variant="secondary" onClick={onRevealWorkspace} disabled={!workspaceReady} icon={false}>Revelar en Explorador</Button>
          </div>
        </div>
        <div className="input-row">
          <input className="token-input" value={selectedWorkspacePath || ""} readOnly type="text" />
        </div>
        <p className="account-note">{workspaceCustomized ? "Carpeta elegida manualmente. Cambiar alias ya no la reemplaza." : "Mientras no cambies la carpeta manualmente, el alias recalcula esta ruta."}</p>
      </div>

      {needsGithub ? <AccountBox ready={githubReady} userName={github?.userName} onConfigure={onConfigureGithub} onChange={onChangeGithub} busy={busyAction === "github"} /> : null}
      {needsExercism ? <ExercismBox ready={exercismReady} token={exercismToken} setToken={setExercismToken} onConfigure={onConfigureExercism} busy={busyAction === "exercism"} /> : null}

      <div className="account-box">
        <div className="account-row account-row-top">
          <div className="account-title"><ToolLogo icon="vscode" /><div><p>Extensión VS Code</p><span>{extensionState.humanDescription || "Valida la activity bar Estudio/Ejercicios, comandos y panel local."}</span></div></div>
          <div className="account-buttons">
            <span className={`status-badge ${statusClass(extensionStatus)}`}>{statusLabel(extensionStatus)}</span>
            <Button variant="secondary" onClick={onReinstallVSCodeExtension} disabled={busyAction === "vscode-extension" || !workspaceConfigured} icon={false}>{busyAction === "vscode-extension" ? <Icons.Loader className="h-4 w-4" /> : null} Reinstalar extensión</Button>
            <Button variant="secondary" onClick={onOpenVSCode} disabled={!workspaceConfigured} icon={false}>Abrir VS Code</Button>
            <Button variant="secondary" onClick={onOpenExercisePanel} disabled={!workspaceConfigured} icon={false}>Abrir panel de ejercicios</Button>
          </div>
        </div>
      </div>

      <div className="account-box">
        <div className="account-row account-row-top">
          <div className="account-title"><ToolLogo icon="workspace" /><div><p>Archivo API Key</p><span>{extensionConfig.humanDescription || "Se usará para configuración local manual de la extensión."}</span></div></div>
          <div className="account-buttons">
            <span className={`status-badge ${statusClass(apiKeyStatus)}`}>{statusLabel(apiKeyStatus)}</span>
            <Button variant="secondary" onClick={onOpenApiKeyConfig} disabled={!workspaceConfigured} icon={false}>Abrir archivo API Key</Button>
            <Button variant="secondary" onClick={onRevealApiKeyConfig} disabled={!workspaceConfigured} icon={false}>Revelar en Explorador</Button>
          </div>
        </div>
        <div className="account-paths">
          <span>{extensionConfig.localConfigPath || "usuario/config/estudio-socratico.extension.local.json"}</span>
          <span>{extensionConfig.exampleConfigPath || "usuario/config/estudio-socratico.extension.example.json"}</span>
        </div>
      </div>
    </div>
    <div className="account-actions">
      <Button onClick={onNext} disabled={!canContinue}>Continuar</Button>
      {!canContinue ? <p>Completa las cuentas requeridas y deja una carpeta de workspace seleccionada.</p> : null}
    </div>
  </SetupPanel>;
}

function AccountBox({ ready, userName, onConfigure, onChange, busy }) {
  return <div className="account-box">
    <div className="account-row">
      <div className="account-title"><ToolLogo icon="github" /><div><p>GitHub</p><span>{ready ? `Conectado como ${userName || "cuenta activa"}` : "Prepara tu fork y remotos del workspace."}</span></div></div>
      {ready
        ? <div className="account-buttons"><span className="status-badge status-ready">Conectado</span><Button variant="secondary" onClick={onChange} disabled={busy} icon={false}>{busy ? <Icons.Loader className="h-4 w-4" /> : null} Cambiar cuenta</Button></div>
        : <div className="account-buttons"><Button variant="secondary" onClick={onConfigure} disabled={busy} icon={false}>{busy ? <Icons.Loader className="h-4 w-4" /> : null} Iniciar sesión</Button><Button variant="secondary" onClick={onChange} disabled={busy} icon={false}>Cambiar cuenta</Button></div>}
    </div>
  </div>;
}

function ExercismBox({ ready, token, setToken, onConfigure, busy }) {
  const paste = async () => {
    if (navigator.clipboard?.readText) {
      const text = await navigator.clipboard.readText();
      setToken(text.trim());
    }
  };

  const openExternal = (action, fallback) => {
    if (isBridgeAvailable()) {
      requestBackend(action).catch(() => window.open(fallback, "_blank", "noopener,noreferrer"));
    } else {
      window.open(fallback, "_blank", "noopener,noreferrer");
    }
  };

  return <div className="account-box">
    <div className="account-row account-row-top">
      <div className="account-title"><ToolLogo icon="exercism" /><div><p>Exercism</p><span>{ready ? "Token configurado y validado." : "Necesario para importar ejercicios del track de C."}</span></div></div>
      <div className="account-buttons">
        <Button variant="secondary" onClick={() => openExternal(BackendAction.OpenExercismTokenPage, "https://exercism.org/settings/api_cli")} icon={false}>Obtener token</Button>
        <Button variant="secondary" onClick={() => openExternal(BackendAction.OpenExercismCTrack, "https://exercism.org/tracks/c")} icon={false}>Abrir track de C</Button>
      </div>
    </div>
    <div className="input-row">
      <input className="token-input" value={token} onChange={e => setToken(e.target.value)} type="password" placeholder={ready ? "Token configurado" : "Token de Exercism"} />
      <Button variant="secondary" onClick={paste} icon={false}>Pegar</Button>
      <Button variant="secondary" onClick={onConfigure} disabled={busy || token.trim().length === 0} icon={false}>{busy ? <Icons.Loader className="h-4 w-4" /> : null} Guardar</Button>
    </div>
  </div>;
}

function ExecuteScreen({
  selectedWorkflow,
  progress,
  currentStep,
  onStart,
  onCancel,
  running,
  finished,
  ready,
  onFinish,
  onExportDiagnostics,
  lastSummary,
  snapshot,
}) {
  const resultIssues = finished ? finalIssues(selectedWorkflow, lastSummary, snapshot) : [];
  const resultDetails = finished ? finalReadiness(lastSummary, snapshot) : null;
  const uninstallReport = selectedWorkflow === "uninstall" ? lastSummary?.uninstallReport || lastSummary : null;
  return <SetupPanel>
    <HeaderBlock eyebrow={finished ? "Resultado" : "Aplicación"} title={finished ? finalTitle(selectedWorkflow, ready, lastSummary, snapshot) : selectedWorkflow === "uninstall" ? "Desinstalar Estudio Socrático" : "Listo para configurar"} text={finished ? finalText(selectedWorkflow, ready, lastSummary, snapshot) : selectedWorkflow === "uninstall" ? "Se quitarán herramientas y configuración gestionadas por el instalador. Tu trabajo de estudiante se conservará salvo una opción peligrosa explícita futura." : "El configurador instalará y reparará lo necesario según el flujo seleccionado."} />
    <div className="progress-section">
      <div className="progress-meta"><span>{running ? currentStep : finished ? "Proceso terminado" : "Esperando confirmación"}</span><span>{progress}%</span></div>
      <div className="progress-track"><div className={`progress-fill ${running ? "progress-sweep" : ""}`} style={{ width: `${progress}%` }} /></div>
    </div>
    {finished && selectedWorkflow === "uninstall" ? <UninstallPreview report={uninstallReport} /> : null}
    {finished && selectedWorkflow !== "uninstall" ? <div className="result-card">
      {ready && resultDetails
        ? <div className="result-summary">
            <span className="status-badge status-ready">Smoke test F9: passed</span>
            <span className="result-meta">Alias: {resultDetails.alias || "sin definir"}</span>
            <span className="result-meta">GitHub: {resultDetails.githubLogin || "sin cuenta"}</span>
          </div>
        : <ul className="result-list">{resultIssues.map(issue => <li key={issue}>{issue}</li>)}</ul>}
    </div> : null}
    <div className="execute-actions">
      {!running && !finished ? <Button variant={selectedWorkflow === "uninstall" ? "danger" : "primary"} onClick={onStart}>{selectedWorkflow === "uninstall" ? "Desinstalar" : "Aplicar configuración"}</Button> : null}
      {running ? <Button disabled icon={false}><Icons.Loader className="h-4 w-4" /> Trabajando...</Button> : null}
      {running ? <Button variant="secondary" icon={false} onClick={onCancel}>Cancelar</Button> : null}
      {finished ? <Button onClick={onFinish}>{ready && selectedWorkflow !== "uninstall" ? "Abrir VS Code" : "Abrir logs"}</Button> : null}
      <Button variant="secondary" onClick={onExportDiagnostics} icon={false}>Exportar diagnóstico</Button>
    </div>
  </SetupPanel>;
}

function TechConsole({ logs, onClose }) {
  return <div className="console"><div className="console-head"><span>Diagnóstico técnico</span><button onClick={onClose}>×</button></div><div className="console-body">{logs.map((log, index) => <div key={`${log}-${index}`}><span>›</span>{log}</div>)}</div></div>;
}

function screenLabel(screen, workflow) {
  if (screen === "welcome") return "Inicio";
  if (screen === "workflow") return "Elegir flujo";
  if (screen === "scan") return "Revisión";
  if (screen === "components") return workflow === "uninstall" ? "Limpieza" : "Preparación";
  if (screen === "accounts") return "Cuentas";
  if (screen === "execute") return workflow === "uninstall" ? "Limpieza" : "Instalación";
  return "Configuración";
}

export default function App() {
  const [screen, setScreen] = useState("welcome");
  const [workflow, setWorkflow] = useState("setup");
  const [snapshot, setSnapshot] = useState(null);
  const [lastSummary, setLastSummary] = useState(null);
  const [exercismToken, setExercismToken] = useState("");
  const [localAlias, setLocalAlias] = useState("");
  const [selectedWorkspacePath, setSelectedWorkspacePath] = useState("");
  const [workspaceCustomized, setWorkspaceCustomized] = useState(false);
  const [consoleOpen, setConsoleOpen] = useState(false);
  const [logs, setLogs] = useState(["Interfaz cargada.", "Esperando selección de flujo."]);
  const [running, setRunning] = useState(false);
  const [finished, setFinished] = useState(false);
  const [progress, setProgress] = useState(0);
  const [currentStep, setCurrentStep] = useState("Esperando confirmación");
  const [loadingState, setLoadingState] = useState(false);
  const [busyAction, setBusyAction] = useState(null);

  const backendAvailable = isBridgeAvailable();
  const stage = screenLabel(screen, workflow);
  const canGoBack = screen !== "welcome" && !running;
  const tools = useMemo(() => toolsFromSnapshot(snapshot), [snapshot]);
  const ready = summaryIsReady(workflow, lastSummary, snapshot);
  const workspaceReferencePath = snapshot?.recommendedWorkspacePath || snapshot?.workspaceContext?.recommendedWorkspacePath || snapshot?.workspacePath || "";
  const recommendedPath = recommendedWorkspacePath(workspaceReferencePath, localAlias || snapshot?.localAlias || "");
  const effectiveWorkspacePath = selectedWorkspacePath || snapshot?.workspacePath || recommendedPath;

  useEffect(() => {
    if (!localAlias.trim() && snapshot?.localAlias) {
      setLocalAlias(snapshot.localAlias);
    }
  }, [localAlias, snapshot?.localAlias]);

  useEffect(() => {
    if (!snapshot) {
      return;
    }

    if (!selectedWorkspacePath) {
      if (snapshot.workspaceValid && snapshot.workspacePath) {
        setSelectedWorkspacePath(snapshot.workspacePath);
        setWorkspaceCustomized(true);
        return;
      }

      setSelectedWorkspacePath(resolveWorkspaceSelection({
        currentPath: "",
        manual: false,
        alias: localAlias || snapshot.localAlias || "",
        referencePath: workspaceReferencePath,
      }));
      return;
    }

    if (!workspaceCustomized) {
      setSelectedWorkspacePath(resolveWorkspaceSelection({
        currentPath: selectedWorkspacePath,
        manual: false,
        alias: localAlias || snapshot.localAlias || "",
        referencePath: workspaceReferencePath,
      }));
    }
  }, [localAlias, selectedWorkspacePath, snapshot, workspaceCustomized, workspaceReferencePath]);

  const addLog = useCallback((log) => {
    setLogs(prev => [...prev.slice(-120), log]);
  }, []);

  const refreshState = useCallback(async (reason = "Revisión") => {
    if (!isBridgeAvailable()) {
      addLog(`${reason}: bridge WebView2 no disponible.`);
      return null;
    }

    setLoadingState(true);
    try {
      const state = await requestBackend(BackendAction.GetCurrentState, {
        workspacePath: effectiveWorkspacePath,
        localAlias: localAlias.trim() || undefined,
      });
      setSnapshot(state);
      addLog(`${reason}: ${state.globalMessage || state.globalState || "estado recibido"}.`);
      return state;
    } catch (error) {
      addLog(`${reason}: ${messageFromError(error)}`);
      return null;
    } finally {
      setLoadingState(false);
    }
  }, [addLog, effectiveWorkspacePath, localAlias]);

  useEffect(() => {
    const onBridgeEvent = (event) => {
      const evt = event.detail || {};
      const type = bridgeEventName(evt.type);
      const payload = evt.payload || {};

      if (type === "diagnosticcompleted") {
        setSnapshot(payload);
      }

      if (type === "verificationstarted") {
        setRunning(true);
        setFinished(false);
      }

      if (["stepstarted", "stepprogress", "stepneedsuserinput", "stepsucceeded", "stepfailed", "stepskipped"].includes(type)) {
        const nextProgress = Number.isFinite(payload.percent) ? Math.max(0, Math.min(100, Math.round(payload.percent))) : progress;
        setProgress(nextProgress);
        setCurrentStep(payload.message || payload.title || "Trabajando");
        addLog(`${payload.title || "Paso"}: ${payload.message || type}`);
      }

      if (type === "verificationcompleted" || type === "globalstatechanged") {
        setLastSummary(payload);
        setRunning(false);
        setFinished(true);
        setProgress(100);
        setCurrentStep(payload.globalMessage || "Verificación completada");
      }

      if (type === "error") {
        setRunning(false);
        setFinished(true);
        addLog(`Error: ${messageFromError(payload)}`);
      }
    };

    window.addEventListener("estudio-bridge-event", onBridgeEvent);
    refreshState("Diagnóstico inicial");
    return () => window.removeEventListener("estudio-bridge-event", onBridgeEvent);
  }, []);

  const selectWorkflow = (nextWorkflow) => {
    setWorkflow(nextWorkflow);
    setFinished(false);
    setRunning(false);
    setProgress(0);
    setLastSummary(null);
    setCurrentStep("Esperando confirmación");
    addLog(`Flujo seleccionado: ${nextWorkflow}`);
  };

  const handleAliasInput = (value) => {
    const nextAlias = normalizeAliasInput(value);
    setLocalAlias(nextAlias);
    if (!workspaceCustomized) {
      setSelectedWorkspacePath(resolveWorkspaceSelection({
        currentPath: selectedWorkspacePath,
        manual: false,
        alias: nextAlias || snapshot?.localAlias || "",
        referencePath: workspaceReferencePath,
      }));
    }
  };

  const promptAliasChange = () => {
    const proposed = window.prompt("Escribe el alias local que quieres usar.", localAlias || snapshot?.localAlias || "");
    if (proposed !== null) {
      handleAliasInput(proposed);
    }
  };

  const promptWorkspaceChange = () => {
    const proposed = window.prompt("Escribe la carpeta del workspace.", effectiveWorkspacePath || recommendedPath);
    if (proposed === null) {
      return;
    }

    const nextPath = proposed.trim();
    if (!nextPath) {
      return;
    }

    setSelectedWorkspacePath(nextPath);
    setWorkspaceCustomized(true);
    addLog(`Workspace seleccionado manualmente: ${nextPath}`);
  };

  const goBack = () => {
    const index = SCREEN_ORDER.indexOf(screen);
    if (index <= 0) return;
    if (screen === "execute") {
      setFinished(false);
      setProgress(0);
      setLastSummary(null);
    }
    const previous = SCREEN_ORDER[index - 1];
    if (previous === "accounts" && workflow === "uninstall") setScreen("components");
    else setScreen(previous);
  };

  const configureGithub = async (change = false) => {
    setBusyAction("github");
    try {
      const action = change ? BackendAction.ChangeGithubAccount : BackendAction.ConfigureGithub;
      const account = await requestBackend(action, { workspacePath: effectiveWorkspacePath });
      setSnapshot(prev => prev ? { ...prev, gitHub: account } : prev);
      addLog(change ? "Cuenta GitHub cambiada." : "Cuenta GitHub configurada.");
      await refreshState("Revisión GitHub");
    } catch (error) {
      addLog(`GitHub: ${messageFromError(error)}`);
    } finally {
      setBusyAction(null);
    }
  };

  const configureExercism = async () => {
    if (!exercismToken.trim()) return;
    setBusyAction("exercism");
    try {
      const account = await requestBackend(BackendAction.ConfigureExercism, {
        exercismToken: exercismToken.trim(),
        workspacePath: effectiveWorkspacePath,
      });
      setSnapshot(prev => prev ? { ...prev, exercism: account } : prev);
      setExercismToken("");
      addLog("Exercism configurado. Token no registrado en consola.");
      await refreshState("Revisión Exercism");
    } catch (error) {
      addLog(`Exercism: ${messageFromError(error)}`);
    } finally {
      setBusyAction(null);
    }
  };

  const runWorkspaceAction = async (key, action, successMessage, refreshLabel = null) => {
    setBusyAction(key);
    try {
      await action();
      if (successMessage) {
        addLog(successMessage);
      }
      if (refreshLabel) {
        await refreshState(refreshLabel);
      }
    } catch (error) {
      addLog(`${successMessage || key}: ${messageFromError(error)}`);
    } finally {
      setBusyAction(null);
    }
  };

  const exportDiagnostics = async () => {
    try {
      const result = await requestBackend(BackendAction.ExportDiagnostics, { workspacePath: effectiveWorkspacePath });
      addLog(`Diagnóstico exportado: ${result.path || "ruta no disponible"}`);
    } catch (error) {
      addLog(`Exportar diagnóstico: ${messageFromError(error)}`);
    }
  };

  const cancelWorkflow = async () => {
    try {
      await requestBackend(BackendAction.CancelPlan);
      setRunning(false);
      setCurrentStep("Cancelado");
      addLog("Operación cancelada.");
    } catch (error) {
      addLog(`Cancelar: ${messageFromError(error)}`);
    }
  };

  const startExecution = async () => {
    if (!backendAvailable) {
      addLog("No hay bridge WebView2 disponible para ejecutar acciones reales.");
      setFinished(true);
      setLastSummary({ succeeded: false, globalMessage: "Bridge WebView2 no disponible." });
      return;
    }

    setRunning(true);
    setFinished(false);
    setProgress(0);
    setLastSummary(null);
    setCurrentStep("Iniciando flujo");

    try {
      if (workflow === "accounts" && exercismToken.trim()) {
        await requestBackend(BackendAction.ConfigureExercism, {
          exercismToken: exercismToken.trim(),
          workspacePath: effectiveWorkspacePath,
        });
        setExercismToken("");
      }

      if (workflow === "uninstall") {
        const confirmed = window.confirm("Desinstalar Estudio Socrático quitará herramientas y configuración gestionadas por el instalador. Ejercicios, usuario, logs y API keys locales se conservarán. ¿Continuar?");
        if (!confirmed) {
          setRunning(false);
          setCurrentStep("Cancelado");
          addLog("Desinstalación cancelada por el usuario.");
          return;
        }
      }

      const action =
        workflow === "reinstall" ? BackendAction.ReinstallManaged :
        workflow === "uninstall" ? BackendAction.UninstallManaged :
        workflow === "accounts" ? BackendAction.RunSmokeTest :
        BackendAction.ApplyWorkflow;

      const payload = {
        mode: modeForWorkflow(workflow),
        exercismToken: exercismToken.trim() || undefined,
        localAlias: localAlias.trim() || undefined,
        workspacePath: effectiveWorkspacePath,
        allowAggressiveCleanup: false,
        dryRun: workflow === "uninstall" ? false : undefined,
      };

      const result = await requestBackend(action, payload);
      const normalizedResult = workflow === "uninstall" && !result.uninstallReport
        ? { succeeded: true, globalMessage: result.message, uninstallReport: result }
        : result;
      setLastSummary(normalizedResult);
      setFinished(true);
      setRunning(false);
      setProgress(100);
      setCurrentStep(normalizedResult.globalMessage || "Proceso terminado");
      addLog(`Backend finalizó ${action}.`);
      await refreshState("Revisión final");
      if (workflow !== "uninstall" && normalizedResult.succeeded) {
        await requestBackend(BackendAction.OpenVSCode, { workspacePath: effectiveWorkspacePath });
        addLog("VS Code abierto al finalizar.");
      }
    } catch (error) {
      setRunning(false);
      setFinished(true);
      setLastSummary({ succeeded: false, globalMessage: messageFromError(error) });
      addLog(`Backend error: ${messageFromError(error)}`);
    }
  };

  const finishAction = async () => {
    if (ready && workflow !== "uninstall") {
      try {
        await requestBackend(BackendAction.OpenVSCode, { workspacePath: effectiveWorkspacePath });
        addLog("VS Code abierto.");
      } catch (error) {
        addLog(`Abrir VS Code: ${messageFromError(error)}`);
      }
    } else {
      try {
        await requestBackend(BackendAction.OpenLogs);
      } catch (error) {
        addLog(`Abrir logs: ${messageFromError(error)}`);
      }
    }
  };

  return <AppShell stage={stage} canGoBack={canGoBack} onBack={goBack} consoleOpen={consoleOpen} setConsoleOpen={setConsoleOpen} logs={logs}>
    {screen === "welcome" ? <Welcome onNext={() => setScreen("workflow")} /> : null}
    {screen === "workflow" ? <WorkflowScreen selectedWorkflow={workflow} setSelectedWorkflow={selectWorkflow} onNext={() => setScreen("scan")} /> : null}
    {screen === "scan" ? <ScanScreen selectedWorkflow={workflow} tools={tools} loading={loadingState} backendAvailable={backendAvailable} onRefresh={() => refreshState("Revisión manual")} onNext={() => setScreen("components")} /> : null}
    {screen === "components" ? <ComponentsScreen selectedWorkflow={workflow} onNext={() => setScreen(workflow === "uninstall" ? "execute" : "accounts")} /> : null}
    {screen === "accounts" ? <AccountsScreen
      selectedWorkflow={workflow}
      snapshot={snapshot}
      github={snapshot?.gitHub}
      exercism={snapshot?.exercism}
      exercismToken={exercismToken}
      setExercismToken={setExercismToken}
      localAlias={localAlias}
      onAliasInput={handleAliasInput}
      onChangeAlias={promptAliasChange}
      selectedWorkspacePath={effectiveWorkspacePath}
      recommendedPath={recommendedPath}
      workspaceCustomized={workspaceCustomized}
      onChangeWorkspace={promptWorkspaceChange}
      onRevealWorkspace={() => runWorkspaceAction(
        "workspace",
        () => requestBackend(BackendAction.RevealInExplorer, { path: effectiveWorkspacePath }),
        "Workspace revelado en el Explorador.",
      )}
      onConfigureGithub={() => configureGithub(false)}
      onChangeGithub={() => configureGithub(true)}
      onConfigureExercism={configureExercism}
      onReinstallVSCodeExtension={() => runWorkspaceAction(
        "vscode-extension",
        () => requestBackend(BackendAction.ReinstallVSCodeExtension, { workspacePath: effectiveWorkspacePath }),
        "Extensión local de VS Code reinstalada.",
        "Revisión extensión VS Code",
      )}
      onOpenVSCode={() => runWorkspaceAction(
        "open-vscode",
        () => requestBackend(BackendAction.OpenVSCode, { workspacePath: effectiveWorkspacePath }),
        "VS Code abierto.",
      )}
      onOpenExercisePanel={() => runWorkspaceAction(
        "exercise-panel",
        () => requestBackend(BackendAction.OpenExercisePanel, { workspacePath: effectiveWorkspacePath }),
        "Panel de ejercicios solicitado en VS Code.",
      )}
      onOpenApiKeyConfig={() => runWorkspaceAction(
        "api-key-open",
        () => requestBackend(BackendAction.OpenExtensionApiKeyConfig, { workspacePath: effectiveWorkspacePath }),
        "Archivo local de API Key abierto.",
        "Revisión archivo API Key",
      )}
      onRevealApiKeyConfig={() => runWorkspaceAction(
        "api-key-reveal",
        () => requestBackend(BackendAction.RevealExtensionApiKeyConfig, { workspacePath: effectiveWorkspacePath }),
        "Archivo local de API Key revelado.",
      )}
      onNext={() => setScreen("execute")}
      busyAction={busyAction}
    /> : null}
    {screen === "execute" ? <ExecuteScreen selectedWorkflow={workflow} progress={progress} currentStep={currentStep} running={running} finished={finished} ready={ready} onStart={startExecution} onCancel={cancelWorkflow} onFinish={finishAction} onExportDiagnostics={exportDiagnostics} lastSummary={lastSummary} snapshot={snapshot} /> : null}
  </AppShell>;
}
