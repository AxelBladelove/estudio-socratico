# Proceso de Release: Estudio Socrático 2.0

Este documento detalla el flujo de trabajo de releases, la estrategia de versionado y la automatización mediante GitHub Actions para el instalador y configurador de **Estudio Socrático 2.0**.

---

## 1. Estrategia de Versionado Dual

Para garantizar la estabilidad del branding público y a la vez cumplir con los requisitos técnicos de Windows Installer (MSI/Burn) de mantener versiones estrictamente crecientes (monotónicas) para Major Upgrades, el proyecto separa el versionado en dos conceptos:

### Versión Pública (`PublicDisplayVersion`)
- **Valor:** `"2.0"` (Branding estable).
- **Ámbito:** Títulos de UI, documentación (READMEs), notas de versión y títulos de releases en GitHub (ej. *Estudio Socrático 2.0 Stable*).
- **Objetivo:** Ofrecer una marca limpia y unificada para los estudiantes.

### Versión Técnica (`InternalPackageVersion` / `ProductVersion`)
- **Valor:** `"2.0.15"`, `"2.0.16"`, etc. (Monotónica creciente).
- **Ámbito:** `ProductVersion` en archivos `.wixproj` (MSI y Burn Bundle), versión de ensamblado .NET, checks internos de actualización, y etiquetas de Git (tags).
- **Objetivo:** Permitir que Windows Installer detecte e instale la nueva versión sobre la anterior reemplazando los binarios sin forzar una desinstalación manual.

---

## 2. Automatización con `set-version.ps1`

Para automatizar la propagación de versiones antes de publicar, se utiliza el script de PowerShell ubicado en `_estudio/installer/scripts/set-version.ps1`.

### Uso
```powershell
.\_estudio\installer\scripts\set-version.ps1 `
  -PublicDisplayVersion "2.0" `
  -InternalPackageVersion "2.0.16"
```

El script actualiza de manera coordinada:
1. `<ProductVersion>` en `Directory.Build.props`.
2. `PublicDisplayVersion`, `Version` y `SetupFileName` en `ProductInfo.cs`.
3. Metadatos de versión en archivos `.wixproj` de empaquetado.
4. Versión en el archivo `package.json` de la raíz del proyecto.
5. Referencias de nombres de instaladores y marcas en `README.md` y guías.

---

## 3. GitHub Actions para Releases Automáticos

El workflow de CI/CD configurado en `.github/workflows/release-installer.yml` automatiza la compilación del instalador y su publicación en GitHub Releases.

### Desencadenantes (Triggers)
- **Tag push:** Cuando se publica una etiqueta que coincide con el patrón `v*` (ej. `v2.0.16`).
- **Manual:** Mediante el botón *Run workflow* en la pestaña Actions de GitHub (`workflow_dispatch`).

### Flujo de Ejecución (Jobs)
1. **Clonado del Repositorio:** Descarga el código en un ejecutor `windows-latest`.
2. **Setup de Entornos:** Configura .NET 10 y Bun.
3. **Build Frontend:** Instala dependencias con `bun ci` y compila la interfaz React (`bun run build`). Copia los archivos estáticos al subdirectorio `wwwroot` de la aplicación WinUI.
4. **Restauración y Tests:** Ejecuta `dotnet restore` y pasa la suite de pruebas unitarias (`dotnet test`).
5. **Compilación y Publicación .NET:** Compila la aplicación WinUI (`EstudioSocratico.Configurator.App`) y el worker con privilegios (`EstudioSocratico.Configurator.Elevated`) como aplicaciones self-contained para `win-x64`.
6. **Compilación WiX:** Compila el paquete MSI y el cargador de arranque Burn Bundle (`Estudio-Socratico-Setup-v*.exe`).
7. **Firma SHA256:** Genera el archivo hash `.sha256` para el instalador exe.
8. **GitHub Release:** Crea o actualiza la release oficial cargando el instalador y su firma como assets.

---

## 4. Políticas de Seguridad de Actualizaciones

El actualizador del configurador valida estrictamente los paquetes antes de ejecutarlos:
1. **Host Oficial:** Solo se descargan recursos que provengan de `https://github.com/AxelBladelove/estudio-socratico/releases/download/`.
2. **Nombres de Asset Validados:** Los nombres de archivos descargados deben cumplir estrictamente con los patrones regex esperados (`Estudio-Socratico-Setup-v{version}-x64.exe` y su `.sha256` correspondiente).
3. **Verificación de Firma:** Se descarga y parsea el archivo de firma. Luego se computa el SHA256 del `.exe` localmente. Si no coincide, los archivos se eliminan y la actualización se cancela.
4. **Protección de Datos:** La instalación encima conserva intacta la carpeta `usuario/` (logs, alias e historial) y el workspace con el trabajo práctico del estudiante.

## 5. Release Estable 2.0.15

`v2.0.15` cierra la etapa inicial estable de Estudio Socrático 2.0. Antes de
publicar debe validarse:

- banner visible de actualización en pantalla principal;
- boton **Actualizar ahora** usando `CheckForUpdates` y `TriggerUpdate`;
- F9 registrado por la extensión de VS Code;
- `Ctrl+Shift+B` operativo como build task por defecto;
- extensión instalada como `estudio-socratico.estudio-exercism@2.0.16`;

## 6. Hotfix VS Code BYOK 2.0.16

`v2.0.16` corrige la ruta BYOK de la extensión de VS Code. El archivo
`usuario/config/estudio-socratico.extension.local.json` queda como fuente
principal para `provider`, `apiKey`, `model` y `features`; las variables de
entorno de Gemini quedan solo como fallback opcional.
- MSI/Burn generados por WiX y publicados solo por GitHub Actions.
