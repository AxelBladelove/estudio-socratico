# Setup Windows

Esta carpeta contiene el instalador de Estudio Socratico.

Desde Fase 4, la experiencia principal del release ya no es Textual ni la TUI.
El ejecutable visible del usuario es la app Windows nativa `Estudio.Setup.Windows`
publicada como `Instalar Estudio Socrático.exe`, montada sobre el engine
`desired-state`.

## Entrada Recomendada

```bat
_estudio\setup\Estudio.Setup.cmd
```

En un ZIP de release, doble clic en `Estudio.Setup.cmd` o `Estudio.Setup.exe`
ya no es la ruta recomendada. El release limpio expone:

```text
Instalar Estudio Socrático.exe
README.txt
payload/
```

Ese comando esta pensado para ejecutarse desde una terminal abierta en la raiz
del repo. En desarrollo sigue usando `dotnet run` para el backend cuando no
existen ejecutables publicados. En release, el usuario final ve solo el
instalador Windows principal y un `payload/` con framework, VSIX y hashes.

Sin argumentos, el instalador arranca en `verify`, ejecuta un diagnostico
automatico y luego te deja elegir `Instalar`, `Actualizar`, `Reinstalar`,
`Desinstalar` o volver a `Verificar` desde la misma UI.

## Cambio De Identidad

Desde la TUI:

- `Cambiar GitHub` fuerza `gh auth logout` + `gh auth login --web`, vuelve a
  resolver el usuario autenticado y repara fork/remotes con esa cuenta.
- `Aplicar alias` usa el valor escrito en el campo `Alias`, intenta renombrar
  el fork `estudio-socratico-<alias-viejo>` a `estudio-socratico-<alias-nuevo>`
  cuando existe y luego actualiza `.estudio_usuario`.
- Si `.estudio_usuario`, `ESTUDIO_USUARIO`, Git local o la configuracion global
  de Exercism ya existen, la TUI precarga alias y token automaticamente.

Desde CLI:

```bat
_estudio\setup\Estudio.Setup.cmd update --change-github
_estudio\setup\Estudio.Setup.cmd update --alias nuevo_alias
```

## Reinstalar Y Desinstalar

```bat
_estudio\setup\Estudio.Setup.cmd reinstall --tui
_estudio\setup\Estudio.Setup.cmd uninstall --tui
```

`reinstall` fuerza la fase reparable de cada componente y luego verifica. Es el
modo correcto cuando la extension, settings o alias quedaron a medias.

`uninstall` es idempotente y prudente: elimina integraciones locales de Estudio
como extension VS Code, claves propias en `settings.json`, `.estudio_usuario`,
config local Gemini y entradas PATH agregadas por el setup. No desinstala Git,
GitHub CLI, VS Code, Node, PowerShell ni MSYS2.

## Que Hace

- valida que el repo este completo;
- valida el alias local del estudiante y permite cambiarlo desde TUI o `--alias`;
- valida GitHub CLI y abre el flujo web cuando falta sesion o decides cambiar
  de cuenta;
- resuelve usuario y correo de GitHub desde `gh auth` sin pedirlos a mano;
- usa el alias como nombre local de Git para los commits de ese clon;
- configura Git local;
- crea `.estudio_usuario`;
- prepara `usuario/errores.md`;
- crea o reutiliza el fork `estudio-socratico-<alias>` y configura remotes;
- instala o valida herramientas base;
- instala o valida Exercism CLI;
- configura Exercism CLI con el token pegado o precargado en la TUI;
- verifica que el track C este listo descargando `hello-world`;
- instala o valida GCC/MSYS2;
- instala `make` y `mingw32-make` para los tests de Exercism C;
- compila herramientas locales del framework;
- empaca e instala la extension local de ejercicios;
- configura `F9` para compilar desde VS Code.
- si falta un token o clave, muestra el enlace correcto, permite abrirlo y pega
  el valor dentro de la misma TUI.

## Exercism

El setup no guarda tokens en el repo. La TUI muestra el enlace oficial:

```text
https://exercism.org/settings/api_cli
```

El estudiante copia su token desde esa pagina, lo pega en el campo
`Exercism Token` y reintenta los pasos fallidos si la instalacion ya habia
avanzado. El backend guarda el token en la configuracion global de Exercism CLI
de esa PC y valida el track C descargando `hello-world`.

Si la PC ya tenia un token global de Exercism, la TUI lo muestra precargado.

Si Exercism responde que la cuenta aun no esta unida al track C, el setup abre:

```text
https://exercism.org/tracks/c
```

Despues de pulsar `Join the C Track` en la web, usa `Fallidos` en la TUI para
continuar desde ese punto.

Para traducir README al importar ejercicios, el backend usa primero la clave
compartida del proyecto en:

```text
_estudio/soporte/exercism/config.json
```

Si el repo no trae clave compartida, puedes configurar una clave local:

```powershell
setx GEMINI_API_KEY "TU_CLAVE"
```

Luego abre una terminal nueva antes de importar.

## Modo Verificacion

```bat
_estudio\setup\Estudio.Setup.cmd verify
```

## Engine Desired-State Opt-In

Mientras conviven el flujo legacy y el engine nuevo por nodos, puedes activar
el engine nuevo desde CLI con cualquiera de estas opciones:

```bat
_estudio\setup\Estudio.Setup.cmd install --desired-state
_estudio\setup\Estudio.Setup.cmd verify --engine desired-state
```

Ese modo ejecuta bloques de alto nivel como GitHub, workspace, VS Code,
extension, compilador y ejercicios sin volver a tratar el ZIP como workspace.
La TUI Terminal.Gui sigue viva como fallback de diagnostico para legacy. La UI
Windows del release usa `desired-state` como camino principal.

## Empaquetar Release

```bat
_estudio\setup\Estudio.Setup.cmd package
```

Genera `_estudio/setup/Estudio.Setup/publish/release/` con carpeta limpia, ZIP
y `payload/manifest.json` con hashes SHA-256. El root del ZIP queda limpio para
usuario final: `Instalar Estudio Socrático.exe`, `README.txt` y `payload/`.
`payload/` contiene el framework comprimido, el VSIX ya empaquetado y
`checksums.sha256`. El usuario final no necesita Python ni Node en su PC.

## Archivos

| Archivo | Rol |
|---|---|
| `Estudio.Setup.cmd` | Wrapper principal para desarrollo dentro del repo |
| `Instalar Estudio Socrático.exe` | Entrada visible del release final |
| `Estudio.Setup.Windows` | UI Windows nativa del instalador 2.0 |
| `payload/` | Framework comprimido, VSIX y hashes de verificacion |
| `Estudio.Setup.exe` | Backend 2.0 self-contained usado durante desarrollo/pruebas |
| `textual/` | Fuente de la UI Textual y pruebas unitarias |
| `Estudio.Setup/` | Backend C# y fallback Terminal.Gui |
| `instalar.cmd`, `instalar.ps1` y modulos `.ps1` | Legacy conservado solo para referencia/compatibilidad |

## Legacy

Los scripts PowerShell 1.x quedan congelados. No se eliminan para no romper
clones antiguos ni documentacion historica, pero la ruta activa para 2.0 es
`Estudio.Setup.cmd` dentro del repo y `Instalar Estudio Socrático.exe` en los
releases. Las mejoras de sistema deben entrar al backend C#; las mejoras
visuales futuras deben entrar en la UI Windows dedicada.
