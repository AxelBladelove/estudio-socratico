# Estudio Socratico Configurador v2

Este directorio contiene el instalador/configurador v2 de Estudio Socratico.
El artefacto final de GitHub Releases es:

```text
Estudio-Socratico-Setup-v2.0.0-x64.exe
```

## Stack

- Bootstrapper: WiX Burn.
- UI principal: WinUI 3.
- Plataforma UI: Windows App SDK self-contained.
- Lenguaje: C#.
- Runtime: .NET 10 LTS.
- Motor: librerias C# modulares.
- Worker elevado: `EstudioSocratico.Configurator.Elevated.exe`.
- Toolchain C: MSYS2 UCRT64, GCC y Make.

## Estructura

```text
_estudio/installer/
├─ EstudioSocratico.Installer.sln
├─ src/
│  ├─ EstudioSocratico.Configurator.App/
│  ├─ EstudioSocratico.Configurator.Core/
│  ├─ EstudioSocratico.Configurator.Engine/
│  ├─ EstudioSocratico.Configurator.Elevated/
│  ├─ EstudioSocratico.Configurator.Cli/
│  └─ EstudioSocratico.Configurator.Tests/
├─ packaging/wix-burn/
└─ docs/
```

## Comandos

```powershell
_estudio/installer/scripts/build-ui.bat
Remove-Item _estudio/installer/artifacts/publish -Recurse -Force -ErrorAction SilentlyContinue
dotnet restore _estudio/installer/EstudioSocratico.Installer.sln
dotnet build _estudio/installer/EstudioSocratico.Installer.sln --configuration Release
dotnet test _estudio/installer/src/EstudioSocratico.Configurator.Tests/EstudioSocratico.Configurator.Tests.csproj --configuration Release
```

Publicacion local de la app:

```powershell
_estudio/installer/scripts/build-ui.bat
Remove-Item _estudio/installer/artifacts/publish/app -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish _estudio/installer/src/EstudioSocratico.Configurator.App/EstudioSocratico.Configurator.App.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=false `
  --output _estudio/installer/artifacts/publish/app
```

El build de release completo vive en `.github/workflows/release-installer.yml`.
No se versionan `.exe`, `.msi`, `.vsix`, `bin/`, `obj/` ni `artifacts/`.

## Estado Persistente

El configurador escribe estado local en:

```text
%LocalAppData%\EstudioSocratico\installer-manifest.json
%LocalAppData%\EstudioSocratico\Logs\
```

El manifest registra dependencias instaladas por Estudio Socratico, PATH antes y
despues, workspace, configuracion de VS Code, GitHub sin secretos, Exercism sin
token y acciones elevadas.

## Decisiones

La UI corre como usuario normal. Solo el worker elevado ejecuta operaciones que
requieren permisos de administrador, y solo acepta operaciones concretas:
`InstallMsys2`, `AddMachinePath`, `InstallWingetPackage`, `RunOfficialInstaller`,
`RemoveManagedDependency` y `RepairPath`.
