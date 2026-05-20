# Arquitectura

El configurador v2 separa la experiencia visual, el motor real y las tareas con
permisos elevados.

## Capas

| Capa | Proyecto | Rol |
|---|---|---|
| UI | `EstudioSocratico.Configurator.App` | WinUI 3 oscuro, progreso por etapas, acciones de instalacion, reparacion, reinstalacion, desinstalacion y diagnostico. |
| Core | `EstudioSocratico.Configurator.Core` | Modelos, errores tipados, manifest, redaccion de secretos y utilidades seguras. |
| Engine | `EstudioSocratico.Configurator.Engine` | Deteccion, instalacion, validacion, logs, manifest, GitHub, Exercism, VS Code, workspace y diagnostico. |
| Elevated | `EstudioSocratico.Configurator.Elevated` | Worker separado con manifest `requireAdministrator`. |
| CLI | `EstudioSocratico.Configurator.Cli` | Diagnostico y automatizacion interna. |
| Packaging | `packaging/wix-burn` | MSI de la app y bundle WiX Burn. |

## Flujo

1. Burn instala o repara el configurador y lo registra en Windows.
2. La app WinUI abre como usuario normal.
3. El motor ejecuta diagnostico real con comandos estructurados.
4. WinGet se usa primero cuando responde y tiene source confiable.
5. Si WinGet falla, se descarga un instalador oficial resuelto desde fuente
   oficial o release oficial.
6. MSYS2 queda orientado a `C:\msys64\ucrt64\bin`.
7. GitHub CLI maneja login web, cambio de cuenta, fork y remotos.
8. Exercism CLI recibe token sin imprimirlo y valida con el track C.
9. VS Code queda con settings, extension local y F9.
10. El manifest decide que puede repararse, reinstalarse o limpiarse.

## Modulos Del Motor

El motor contiene clases con responsabilidad directa:

- `SystemProbe`
- `DependencyDetector`
- `DependencyInstaller`
- `WingetBroker`
- `OfficialInstallerFallback`
- `DownloadManager`
- `ChecksumVerifier`
- `PathManager`
- `EnvironmentManager`
- `Msys2Manager`
- `GccManager`
- `MakeManager`
- `GitManager`
- `GitHubCliManager`
- `GitHubAccountManager`
- `ExercismManager`
- `VSCodeManager`
- `WorkspaceManager`
- `ExtensionManager`
- `GistImporterManager`
- `TelemetryCompatibilityManager`
- `RepairManager`
- `ReinstallManager`
- `UninstallManager`
- `ManifestManager`
- `LogManager`
- `DiagnosticsManager`
- `SecurityManager`

## No Reutilizado

El intento Electron + NSIS fue eliminado. No queda como base, plantilla, UI ni
arquitectura. Los scripts de estudio existentes se conservan cuando son parte
del flujo F9, logs, `conio`, Exercism o Gist.
