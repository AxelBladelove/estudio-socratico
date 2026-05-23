# Estudio Socrático

Estudio Socrático es un entorno guiado para aprender C con VS Code, GitHub,
Exercism y herramientas de compilación configuradas automáticamente.

La meta es estudiar con rigor: compilar, observar errores reales, guardar
intentos y pedir ayuda socrática sin que la IA resuelva los ejercicios por ti.

## Estudio Socrático 2.0

La versión estable actual es `v2.0.16`:

- Instalador visual para Windows.
- Configuración automática de VS Code, GCC/MSYS2, Git, GitHub CLI, Node.js,
  Python y Exercism CLI.
- Workspace listo para estudiar C con logs por intento.
- Compilación con `F9` y alternativa con `Ctrl+Shift+B`.
- Panel de ejercicios en VS Code con Exercism C y catálogos curados.
- Actualizaciones integradas desde GitHub Releases con verificación SHA256.
- API Key local BYOK para proveedores de IA compatibles.

## Instalación

1. Abre la página de GitHub Releases del repositorio.
2. Descarga `Estudio-Socratico-Setup-v2.0.16-x64.exe`.
3. Ejecuta el instalador.
4. Elige **Configurar por primera vez**.
5. Sigue el flujo guiado para GitHub, Exercism, workspace y VS Code.

El instalador conserva tu trabajo. No borra ejercicios, logs, API keys locales
ni repos remotos durante instalación, actualización o reparación normal.

## Actualización

Al abrir Estudio Socrático Instalador, la app consulta GitHub Releases. Si hay
una versión nueva, verás un aviso en la pantalla principal con el botón
**Actualizar ahora**.

También puedes abrir el instalador manualmente y elegir **Actualizar
instalación**. Ese flujo reinstala la extensión gestionada de VS Code si está
vieja y revalida el entorno sin tocar tu trabajo.

## Uso Diario

1. Abre el workspace de Estudio Socrático en VS Code.
2. Crea o abre un archivo `.c`, normalmente dentro de `Ejercicios/`.
3. Presiona `F9`.

`F9` compila el archivo C activo con GCC. Si compila bien, ejecuta el programa
en una consola externa estilo Code::Blocks. Si hay errores, los muestra en la
terminal de VS Code.

`Ctrl+Shift+B` sigue funcionando como alternativa porque ejecuta la tarea de
build por defecto del workspace.

## Si F9 No Responde

1. Confirma que el archivo activo termina en `.c`.
2. Cierra y vuelve a abrir VS Code.
3. Ejecuta **Actualizar instalación** o **Reparar instalación** desde el
   instalador.
4. Prueba `Ctrl+Shift+B` como alternativa.
5. Exporta diagnóstico desde el botón **Diagnóstico técnico** del instalador si
   el problema continúa.

La extensión de VS Code registra `F9` para el comando `Compilar archivo C
activo`. Ese comando usa la misma tarea que `Ctrl+Shift+B` y, si no encuentra la
tarea, llama directamente a `_estudio/soporte/scripts/build.cmd`.

## Exercism

El instalador puede configurar el token de Exercism durante la primera
instalación. Si necesitas hacerlo manualmente:

1. Entra a <https://exercism.org/settings/api_cli>.
2. Copia tu token.
3. Abre el instalador y entra a **Cuentas y ejercicios**.
4. Pega el token en la sección de Exercism.

Para ejercicios de Exercism, `F9` ejecuta los tests oficiales cuando estás
trabajando dentro de un ejercicio importado.

## API Key BYOK

La configuración local de la extensión vive en:

```text
usuario/config/estudio-socratico.extension.local.json
```

Ese archivo no debe subirse al repo. Puedes abrirlo desde el instalador en
**Cuentas y ejercicios** o desde el panel de la extensión. Un ejemplo mínimo:

```json
{
  "provider": "gemini",
  "apiKey": "TU_API_KEY",
  "model": "gemini-2.5-flash"
}
```

## Ayuda Socrática

Puedes pedir ayuda sin entregar la solución completa:

- `@revisar`: pide una pista sobre el error actual.
- `@ver`: pide una prueba de escritorio del código.
- `@sintetizar`: resume patrones de error de la sesión.
- `@test` o `@validar`: genera pruebas locales para ejercicios compatibles.

## Dónde Queda Tu Trabajo

```text
usuario/
├── logs/
├── config/
│   └── estudio-socratico.extension.local.json
└── errores.md
```

Cada compilación registra un intento en `usuario/logs/` y puede crear commits
automáticos para dejar historial de estudio.

## Exportar Diagnóstico

En el instalador, abre **Diagnóstico técnico** y usa **Exportar diagnóstico**.
Ese reporte ayuda a revisar estado de herramientas, versión instalada,
workspace, extensión de VS Code y resultados del smoke test.

## Reportar Errores

Al reportar un problema incluye:

- Versión del instalador.
- Versión de la extensión de VS Code.
- Qué estabas intentando hacer.
- Si `F9`, `Ctrl+Shift+B` o ambos fallan.
- El diagnóstico exportado, sin API keys ni datos sensibles.

Para comprobar la extensión instalada:

```powershell
code --list-extensions --show-versions | findstr estudio-socratico
```

El resultado esperado para esta versión es:

```text
estudio-socratico.estudio-exercism@2.0.16
```

## Desarrollo Del Instalador

Validación local principal:

```powershell
dotnet build _estudio/installer/EstudioSocratico.Installer.sln --configuration Release -m:1
dotnet test _estudio/installer/src/EstudioSocratico.Configurator.Tests/EstudioSocratico.Configurator.Tests.csproj --configuration Release

cd _estudio/installer/ui
npm.cmd run build

cd _estudio/soporte/vscode/estudio-exercism
npm.cmd install
npm.cmd run compile
```

El `.exe` final se genera con WiX/Burn y se publica desde GitHub Actions. No se
deben versionar `.exe`, `.msi`, `.sha256`, `bin/`, `obj/`, `artifacts/`,
`node_modules/`, `dist/`, logs ni archivos locales con API keys.
