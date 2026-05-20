# Estudio Socrático Configurador — Rediseño UX v2.1

## 1. Problemas de la UI actual

### Inconsistencia de estado
- Dice "Estudio Socrático está listo" / "El entorno quedó configurado y validado" mientras muestra `Exercism CLI: Missing` y `VS Code: Broken`.
- El estado global (`Succeeded`) se calcula como `errors.Count == 0`, ignorando que dependencias individuales pueden estar Missing/Broken.
- No hay distinción entre componentes críticos y opcionales.

### Dashboard técnico, no producto
- Muestra 10 dependencias con status chips (`Ready`/`Missing`/`Broken`) como contenido principal.
- Rutas absolutas (`C:\msys64\ucrt64\bin\gcc.exe`) visibles como texto primario.
- Panel de logs siempre visible compitiendo por atención visual.
- Botones deshabilitados sin explicación contextual.
- No hay flujo: todo aparece a la vez sin guiar al usuario.
- Lenguaje técnico (`DependencyStatus.Ready`) en lugar de humano.

### Ausencia de guía
- GitHub: no hay flujo de login/cambio de cuenta con contexto.
- Exercism: no guía al token, no explica por qué.
- VS Code: no explica qué configura ni por qué.
- No hay pantalla de bienvenida ni pantalla final clara.
- No hay plan visible antes de actuar.

### Limitaciones de XAML puro
- Iterar diseño en WinUI XAML es lento.
- No hay animaciones de transición entre estados.
- Componentes custom limitados sin librería de UI web madura.

## 2. Referencias estudiadas

### Visual Studio Installer
- **Patrón adoptado:** Progresive disclosure — workloads simples arriba, componentes individuales detrás de "Detalles". Instalación/Modificar/Reparar como modos explícitos. Progreso por paso con estado individual por componente.
- **No copiamos:** Grid complejo de checkboxes. Selección de componentes individuales (nuestro usuario no elige qué instalar).

### Microsoft Dev Home / WinGet Configuration
- **Patrón adoptado:** Desired-state model: diagnose → plan → apply → verify. Idempotencia. Máquina como "estado deseado" que se puede repetir. Validación antes de actuar.
- **No copiamos:** YAML/DSC exposure al usuario. Complejidad de PowerShell DSC.

### GitHub Desktop
- **Patrón adoptado:** Onboarding claro de cuenta. Cambio de cuenta sin terminal. Clone/fork sin git CLI visible. Microcopy amigable ("Sign in to GitHub").
- **No copiamos:** Toda la UI de commits/branches (no aplica).

### VS Code
- **Patrón adoptado:** Dark theme sobrio (no negro plano). Familiaridad dev. Status bar feel. Jerarquía visual clara con sidebar + content. Command palette mindset para acciones.
- **No copiamos:** Editor chrome. Extensiones marketplace.

### Google Antigravity / herramientas agent-first
- **Patrón adoptado:** Artifacts verificables (diagnóstico exportable). Planes visibles antes de ejecutar. Trazabilidad ("qué hizo, qué falló, evidencia"). No decir "hecho" sin evidencia.
- **No copiamos:** Stack interno no documentado. Agentic loop (no aplica a installer).

### Windows 11 Settings / Microsoft Store
- **Patrón adoptado:** Tarjetas limpias con bordes sutiles. Navegación lateral. Estados visuales con iconos y colores. Micro-copy conciso. Dark mode con fondo `#202020`, no negro absoluto.
- **No copiamos:** Breadcrumbs profundos. Toggle-heavy layouts.

## 3. Decisión técnica final

```
WinUI 3 host (mínimo) → WebView2 embebido → React + TypeScript + Tailwind CSS
```

**Por qué no Tauri:** Ya existe backend C# funcional, worker elevado, packaging WiX, pipeline CI. Migrar a Rust sería reescribir el motor solo para mejorar la capa visual.

**Por qué no XAML puro:** Iteración lenta, ecosistema de componentes limitado, animaciones complejas difíciles, no aprovecha skills web existentes.

**Por qué WebView2:** Viene incluido en Windows 10/11. Permite React/TypeScript con hot reload durante dev. UI premium más rápida de construir. Bridge tipado C# ↔ JS.

## 4. Cambios necesarios en backend

### 4.1 Estado global derivado (nuevo)

El engine actual devuelve `SetupSummary.Succeeded = errors.Count == 0`. Nuevo modelo:

```
enum GlobalState:
  Analyzing
  NeedsSetup         // ≥1 critical dependency Missing
  NeedsRepair        // ≥1 critical dependency Broken/Outdated
  NeedsAuthentication // GitHub/Exercism need login
  NeedsUserAction    // User must paste token, enter alias, etc.
  ReadyToConfigure   // Diagnosed, plan ready, waiting for user approval
  Configuring        // Plan is being applied
  PartiallyReady     // Optional items missing, criticals OK
  ReadyToStudy       // ALL criticals Ready, workspace valid, F9 validated
  Failed             // Unrecoverable error
```

Rules:
- `ReadyToStudy` requires ALL of: Git ✓, GH CLI ✓, GH auth ✓, Exercism CLI ✓, VS Code ✓, MSYS2 ✓, GCC ✓, Make ✓, Workspace valid, F9 validated.
- Any critical Missing/Broken → `NeedsSetup` or `NeedsRepair`.
- GitHub/Exercism not authenticated → `NeedsAuthentication`.

### 4.2 Resource model (evolution, not rewrite)

Current managers (`DependencyDetector`, `DependencyInstaller`, `GitHubAccountManager`, `ExercismManager`, `VSCodeManager`, `WorkspaceManager`) already implement detect/install/repair per dependency. Evolution:

1. Add `IResource` interface: `DetectAsync()`, `PlanActions()`, `ApplyAsync()`, `VerifyAsync()`.
2. Wrap existing managers as resources (adapter pattern, not rewrite).
3. New resources: `GitHubAuthResource`, `ExercismAuthResource`, `WorkspaceResource`, `BuildFlowResource`.
4. Each resource reports its own `ResourceStatus` with human-readable messages.

### 4.3 Setup plan (new)

Before applying, generate a visible plan:

```json
{
  "actions": [
    {"id": "install-exercism", "title": "Instalar Exercism CLI", "category": "tools", "severity": "critical", "requiresAdmin": false},
    {"id": "repair-vscode", "title": "Reparar VS Code", "category": "tools", "severity": "critical", "requiresAdmin": false},
    {"id": "auth-github", "title": "Iniciar sesión en GitHub", "category": "auth", "severity": "critical", "requiresUser": true}
  ]
}
```

### 4.4 WebView2 bridge (new)

Typed JSON message protocol over `PostWebMessageAsJson` / `WebMessageReceived`:

**UI → Backend (requests):**
```
diagnoseEnvironment, createSetupPlan, applyPlan, cancelPlan,
repairComponent, configureGithub, changeGithubAccount,
configureExercism, openExercismTokenPage, configureWorkspace,
openVSCode, openLogs, exportDiagnostics, runSmokeTest
```

**Backend → UI (events):**
```
diagnosticStarted, diagnosticUpdated, diagnosticCompleted,
planCreated, stepStarted, stepProgress, stepNeedsUserInput,
stepSucceeded, stepFailed, stepSkipped,
verificationStarted, verificationCompleted,
globalStateChanged, logUpdated
```

**Prohibited:** `runCommand`, `runPowerShell`, `executeRaw`, `shell`.

### 4.5 Honest state messaging

| State | Message |
|---|---|
| Analyzing | "Revisando tu entorno..." |
| NeedsSetup | "Faltan herramientas para empezar." |
| NeedsRepair | "Hay componentes que necesitan reparación." |
| NeedsAuthentication | "Necesitamos conectar tus cuentas." |
| NeedsUserAction | "Necesitamos información tuya para continuar." |
| ReadyToConfigure | "Tu plan de configuración está listo." |
| Configuring | "Configurando tu entorno de estudio..." |
| PartiallyReady | "Puedes empezar, pero faltan pasos recomendados." |
| ReadyToStudy | "Todo está listo para estudiar C." |
| Failed | "No pudimos completar la configuración." |

## 5. Cambios necesarios en UI

### 5.1 Project structure

```
_estudio/installer/ui/
├─ package.json
├─ vite.config.ts
├─ tsconfig.json
├─ tailwind.config.ts
├─ index.html
├─ src/
│  ├─ main.tsx
│  ├─ App.tsx
│  ├─ bridge/
│  │  ├─ types.ts          # Shared types matching C# models
│  │  ├─ bridge.ts         # PostMessage wrapper
│  │  └─ useBridge.ts      # React hook for bridge events
│  ├─ state/
│  │  └─ store.ts          # Zustand store for global state
│  ├─ screens/
│  │  ├─ Welcome.tsx
│  │  ├─ Diagnosis.tsx
│  │  ├─ Plan.tsx
│  │  ├─ GitHub.tsx
│  │  ├─ Exercism.tsx
│  │  ├─ Progress.tsx
│  │  └─ Complete.tsx
│  ├─ components/
│  │  ├─ StatusBadge.tsx
│  │  ├─ ResourceCard.tsx
│  │  ├─ StepIndicator.tsx
│  │  ├─ ActionButton.tsx
│  │  ├─ DetailPanel.tsx
│  │  └─ Layout.tsx
│  └─ styles/
│     └─ globals.css
└─ dist/                   # Build output (not versioned)
```

### 5.2 Screens flow

```
Welcome → Diagnosis → Plan → [GitHub] → [Exercism] → Progress → Complete
                                ↕           ↕
                          (user action flows, shown inline or as modal)
```

## 6. Wireframe textual

### Welcome
```
┌─────────────────────────────────────────────────┐
│  🎓                                             │
│  Configuremos tu entorno                        │
│  de Estudio Socrático                           │
│                                                 │
│  Instalaremos las herramientas necesarias,      │
│  conectaremos GitHub y Exercism, y dejaremos    │
│  VS Code listo para estudiar C.                 │
│                                                 │
│  [████ Empezar configuración ████]              │
│                                                 │
│  Diagnóstico  ·  Reparar  ·  Desinstalar       │
│  Ver logs  ·  Exportar diagnóstico              │
└─────────────────────────────────────────────────┘
```

### Diagnosis
```
┌─────────────────────────────────────────────────┐
│  Tu entorno necesita 3 acciones                 │
│                                                 │
│  ✓ Git                          Listo           │
│  ✓ MSYS2 / GCC / Make          Listo           │
│  ✓ GitHub CLI                   Listo           │
│  ! Exercism CLI                 Pendiente       │
│  ✗ VS Code                     Requiere repara… │
│  ○ GitHub                       Necesita login  │
│  ○ Exercism                     Necesita config │
│                                                 │
│  [▸ Ver detalles técnicos]                      │
│                                                 │
│  [████ Continuar ████]                          │
└─────────────────────────────────────────────────┘
```

### Progress
```
┌─────────────────────────────────────────────────┐
│  Configurando tu entorno de estudio             │
│                                                 │
│  Paso 3 de 5 · Instalando Exercism CLI          │
│  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░  58%               │
│                                                 │
│  ✓ Herramientas del sistema                     │
│  ✓ GitHub autenticado                           │
│  ● Exercism CLI — descargando...                │
│  ○ Workspace                                    │
│  ○ Validación final                             │
│                                                 │
│  [▸ Ver log detallado]                          │
│                                                 │
│  [Cancelar]                                     │
└─────────────────────────────────────────────────┘
```

### Complete (success)
```
┌─────────────────────────────────────────────────┐
│  ✓                                              │
│  Todo está listo para estudiar C                │
│                                                 │
│  Tu entorno fue configurado y validado.         │
│  Abre VS Code y empieza con F9.                 │
│                                                 │
│  [████ Abrir VS Code ████]                      │
│                                                 │
│  Ver guía rápida · Exportar diagnóstico         │
└─────────────────────────────────────────────────┘
```

### Complete (partial)
```
┌─────────────────────────────────────────────────┐
│  !                                              │
│  Configuración incompleta                       │
│                                                 │
│  Falta completar:                               │
│  • Exercism CLI no pudo instalarse              │
│    → Reintentar · Instalar manualmente          │
│  • Token de Exercism pendiente                  │
│    → Configurar ahora                           │
│                                                 │
│  [Reintentar todo]  [Abrir VS Code parcial]     │
│  Exportar diagnóstico                           │
└─────────────────────────────────────────────────┘
```

## 7. Plan de implementación

### Fase 1: Backend evolution (~4 archivos nuevos, ~3 modificados)
1. Add `GlobalState` enum and `GlobalStateCalculator` to Core.
2. Add `IResource` interface and `ResourceStatus` to Core.
3. Add `SetupPlan`, `SetupAction` models to Core.
4. Add `BridgeMessage` types to Core (request/response/event).
5. Create `SetupPlanner` in Engine (generates plan from diagnosis).
6. Modify `ConfiguratorEngine` to expose plan/apply/verify as separate steps.
7. Add `GlobalStateCalculator` tests.

### Fase 2: WebView2 bridge (~3 files in App)
1. Add `Microsoft.Web.WebView2` NuGet to App.csproj.
2. Create `WebViewBridge.cs` — handles PostMessage protocol.
3. Replace MainWindow XAML with WebView2 control + minimal chrome.
4. Bridge dispatches requests to `ConfiguratorEngine`, emits events back.

### Fase 3: React UI (~15 files)
1. Initialize Vite + React + TypeScript project in `ui/`.
2. Add Tailwind CSS.
3. Create bridge types matching C# models.
4. Create Zustand store.
5. Build screens: Welcome, Diagnosis, Plan, GitHub, Exercism, Progress, Complete.
6. Build shared components: StatusBadge, ResourceCard, StepIndicator, etc.
7. Design system: colors, typography, spacing, animations.

### Fase 4: Integration & packaging
1. Add `ui/dist/` copy to publish pipeline.
2. Update MSI to include `ui/dist/`.
3. Update CI workflow: `npm ci && npm run build` before dotnet publish.
4. Update `.gitignore` for `ui/node_modules/`, `ui/dist/`.
5. Build and test full pipeline.

### Fase 5: Validation
1. Build UI + .NET + MSI + Burn.
2. Install and launch.
3. Verify WinUI window with WebView2 rendering React UI.
4. Verify diagnosis doesn't say "listo" with missing deps.
5. Verify plan is shown before applying.
6. Verify progress shows real steps.
7. Verify final screen matches actual state.
8. Verify diagnostics export.
9. Verify uninstall still works.

## 8. Criterios de aceptación

### Visuales
- [ ] Dark theme refinado (fondo ~#0f1419, no negro plano)
- [ ] Tipografía clara con jerarquía (hero 28px+, body 14-16px)
- [ ] Tarjetas con bordes sutiles y padding generoso
- [ ] Estados con iconos y colores (verde=listo, ámbar=pendiente, rojo=error)
- [ ] Logs ocultos por defecto
- [ ] Rutas técnicas ocultas por defecto
- [ ] Animaciones de transición entre screens
- [ ] No texto cortado ni scrollbars feos

### Funcionales
- [ ] Estado global honesto (no "listo" con deps Missing/Broken)
- [ ] Plan visible antes de aplicar
- [ ] Progreso por paso claro
- [ ] GitHub login guiado
- [ ] Exercism token guiado
- [ ] Exportar diagnóstico verificable
- [ ] Abrir VS Code solo si estado lo permite
- [ ] No expone comandos arbitrarios
- [ ] No rompe backend existente
- [ ] MSI/Burn siguen funcionando
- [ ] Launch abre la app con nueva UI

## 9. Diseño visual — Design tokens

```
Background:     #0f1419
Surface:        #151b23
Surface-alt:    #1c2333
Border:         #2d3748
Text-primary:   #e2e8f0
Text-secondary: #8b949e
Text-muted:     #484f58
Accent:         #4fa3ff
Success:        #34d399
Warning:        #f59e0b
Error:          #ef4444
```

Font: `Inter` (Google Fonts) — fallback system-ui.

## 10. Seguridad WebView2

- UI local empaquetada, no contenido remoto.
- Bridge tipado y limitado (whitelist de acciones).
- Validar todos los mensajes recibidos desde WebView.
- No exponer shell, filesystem, worker elevado directamente.
- Secretos manejados solo en C#, nunca en frontend.
- No `localStorage` para tokens.
- `CoreWebView2Settings`: deshabilitar DevTools en release, restringir navegación.
