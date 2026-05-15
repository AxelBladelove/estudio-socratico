# Setup Windows

Esta carpeta contiene el instalador de Estudio Socratico.

## Entrada Recomendada

```bat
_estudio\setup\Estudio.Setup.cmd install --tui
```

Ese comando esta pensado para ejecutarse desde una terminal abierta en la raiz
del repo o desde el ZIP de release. En el repo de desarrollo usa `dotnet run`
para evitar ejecutables viejos; en un paquete limpio usa `Estudio.Setup.exe`
self-contained. La TUI muestra componentes, progreso, log en vivo y permite
reintentar solo los pasos fallidos con `repair --only`.

Si usas los accesos directos de la raiz del repo:

- `Instalar Estudio Socratico.cmd` prepara un clon nuevo: valida GitHub CLI,
  abre la TUI con `install`, valida GitHub CLI, configura alias local y prepara
  el fork/remotes de trabajo.
- `Actualizar Estudio Socratico.cmd` valida la cuenta actual de GitHub CLI,
  abre la TUI con `update` y vuelve a validar/remediar los componentes
  controlados por el framework.

## Que Hace

- valida que el repo este completo;
- pide el alias local del estudiante si hace falta;
- valida GitHub CLI y abre el flujo web cuando falta sesion o decides cambiar
  de cuenta;
- lee `usuario/registro.json` para saber si tu cuenta de GitHub ya tiene rama
  vinculada;
- resuelve usuario y correo de GitHub desde `gh auth` sin pedirlos a mano;
- usa el alias como nombre local de Git para los commits de ese clon;
- configura Git local;
- crea `.estudio_usuario`;
- prepara `usuario/errores.md`;
- crea, activa o renombra la rama personal segun el flujo;
- instala o valida herramientas base;
- instala o valida Exercism CLI;
- instala o valida GCC/MSYS2;
- instala `make` y `mingw32-make` para los tests de Exercism C;
- compila herramientas locales del framework;
- empaca e instala la extension local de ejercicios;
- configura `F9` para compilar desde VS Code.
- si falta un token o clave, muestra el enlace correcto, permite abrirlo y pega
  el valor dentro de la misma TUI.

## Exercism

El setup no guarda tokens en el repo. Solo valida si el CLI global tiene token:

```bat
exercism configure --token TU_TOKEN
```

Cada estudiante configura su token en su propia PC. La extension local usa esa
configuracion para descargar, probar y enviar ejercicios.

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

## Empaquetar Release

```bat
_estudio\setup\Estudio.Setup.cmd package
```

Genera `_estudio/setup/Estudio.Setup/publish/release/` con carpeta limpia, ZIP
y `release-manifest.json` con hashes SHA-256.

## Archivos

| Archivo | Rol |
|---|---|
| `Estudio.Setup.cmd` | Wrapper principal para repo y release |
| `Estudio.Setup/` | Instalador 2.0 en C# con Terminal.Gui |
| `instalar.cmd`, `instalar.ps1` y modulos `.ps1` | Legacy conservado solo para referencia/compatibilidad |

## Legacy

Los scripts PowerShell 1.x quedan congelados. No se eliminan para no romper
clones antiguos ni documentacion historica, pero la ruta activa para 2.0 es
`Estudio.Setup.cmd`. Las mejoras nuevas deben entrar al instalador C#.
