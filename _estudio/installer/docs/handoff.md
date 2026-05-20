# Handoff

## Hecho

- Eliminado el intento Electron + electron-builder + NSIS de `_estudio/installer`.
- Eliminado `.output/`.
- Actualizado `.gitignore` para binarios, MSI, VSIX, `bin/`, `obj/`, `artifacts/`,
  `dist/`, `build/`, logs temporales y caches.
- Creada solucion `EstudioSocratico.Installer.sln`.
- Creados proyectos Core, Engine, WinUI App, Elevated worker, CLI y Tests.
- Implementados modelos, errores tipados, manifest, logs y redaccion de secretos.
- Implementados detectores, WinGet broker, fallback oficial, descarga, checksum,
  PATH, MSYS2, GCC, Make, GitHub, Exercism, VS Code, workspace, Gist, telemetria,
  reparacion, reinstalacion, desinstalacion y diagnostico.
- Implementada UI WinUI 3 oscura con flujo de configuracion, reparacion,
  reinstalacion, desinstalacion, diagnostico, logs y cambio de GitHub.
- Implementado worker elevado con operaciones concretas.
- Agregados tests unitarios de seguridad, versiones, manifest, comandos,
  workspace y desinstalacion segura.
- Agregado packaging WiX MSI + Burn.
- Agregado workflow `.github/workflows/release-installer.yml`.
- Ajustado `compilar_y_grabar.bat` para priorizar UCRT64 y permitir smoke test
  interno sin commit automatico con `ESTUDIO_SKIP_COMMIT=1`.
- Ajustado `.vscode/settings.json` a `C:/msys64/ucrt64/bin/gcc.exe`.

## Como Compilar

```powershell
dotnet restore _estudio/installer/EstudioSocratico.Installer.sln
dotnet build _estudio/installer/EstudioSocratico.Installer.sln --configuration Release
dotnet test _estudio/installer/src/EstudioSocratico.Configurator.Tests/EstudioSocratico.Configurator.Tests.csproj --configuration Release
```

## Como Generar Release

Usar el workflow:

```text
.github/workflows/release-installer.yml
```

Tambien puede ejecutarse manualmente desde GitHub Actions.

## Errores Conocidos

- Este entorno local no tenia `dotnet` en PATH durante la implementacion, asi
  que la compilacion real debe ejecutarse en CI o en una maquina con .NET 10.
- WinUI y WiX dependen de paquetes NuGet restaurados durante CI.

## Decisiones Tecnicas

- El configurador instalado se llama `Estudio Socratico Configurador`, no
  "Installer".
- Burn es entrypoint de release y registra el producto.
- La experiencia principal vive en WinUI, no en la UI estandar de WiX.
- PowerShell se conserva solo donde el framework actual ya lo usa:
  Exercism manager y scripts F9.
- El stack Electron/Tauri/NSIS queda descartado.

## Archivos Clave

- `_estudio/installer/src/EstudioSocratico.Configurator.App/MainWindow.xaml`
- `_estudio/installer/src/EstudioSocratico.Configurator.Engine/ConfiguratorEngine.cs`
- `_estudio/installer/src/EstudioSocratico.Configurator.Engine/DependencyInstallation.cs`
- `_estudio/installer/src/EstudioSocratico.Configurator.Engine/ProductManagers.cs`
- `_estudio/installer/src/EstudioSocratico.Configurator.Elevated/Program.cs`
- `_estudio/installer/packaging/wix-burn/Bundle.wxs`
- `.github/workflows/release-installer.yml`

## Proximo Paso Exacto

Ejecutar el workflow en GitHub Actions o correr localmente con .NET 10 instalado:

```powershell
dotnet test _estudio/installer/src/EstudioSocratico.Configurator.Tests/EstudioSocratico.Configurator.Tests.csproj --configuration Release
```

Luego revisar cualquier error de compilacion de WinUI/WiX que dependa del SDK
instalado y corregirlo sin cambiar el stack.

## Que No Tocar

- `Ejercicios/`
- `usuario/`
- `usuario/logs/`
- `usuario/errores.md`
- `_estudio/soporte/scripts/build.cmd`
- `_estudio/soporte/scripts/compilar_y_grabar.bat`, salvo cambios compatibles
  con F9 y telemetria
- `_estudio/soporte/exercism/manager.ps1`
- `_estudio/soporte/vscode/estudio-exercism/`

## Descartado

- Electron
- Tauri
- NSIS
- electron-builder
- scripts antiguos de `_estudio/setup` como base de instalacion
