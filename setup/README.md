# Setup Windows

Esta carpeta contiene el instalador de Estudio Socratico.

## Entrada Recomendada

```bat
setup\instalar.cmd
```

Ese comando esta pensado para ejecutarse desde una terminal abierta en la raiz
del repo. Por defecto abre un asistente interactivo en PowerShell: instala y
valida todo lo necesario sin pedirte elegir componentes. La TUI solo se detiene
cuando necesita intervencion humana: alias local del estudiante, token de
Exercism, abrir enlaces utiles o recargar el PATH de la terminal. El usuario y
el correo de GitHub se resuelven automaticamente desde `gh auth`. Ese alias se
usa tambien como `user.name` local para que los commits salgan con ese nombre.

Si usas los accesos directos de la raiz del repo:

- `Instalar Estudio Socratico.cmd` prepara un clon nuevo: valida GitHub CLI,
  consulta `usuario/registro.json`, reutiliza la rama ya vinculada a esa
  cuenta de GitHub si existe, pide alias si falta y crea o activa la rama
  personal.
- `Actualizar Estudio Socratico.cmd` valida la cuenta actual de GitHub CLI,
  permite conservarla o cambiarla por navegador, y luego permite conservar o
  renombrar el alias local. Si el alias cambia para la misma cuenta de GitHub,
  tambien se intenta renombrar la carpeta `usuario` y la rama local
  vinculada, en vez de crear una rama nueva. Si existe `origin/<alias_antiguo>`,
  la TUI pregunta antes de subir la nueva rama y borrar la antigua en GitHub.

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
soporte/exercism/config.json
```

Si el repo no trae clave compartida, puedes configurar una clave local:

```powershell
setx GEMINI_API_KEY "TU_CLAVE"
```

Luego abre una terminal nueva antes de importar.

## Modo Verificacion

```bat
setup\instalar.cmd -SoloVerificar -SinWinget -SinExtensiones
```

## Modo No Interactivo

```bat
setup\instalar.cmd -SinOnboarding -UsuarioSlug axel -GitHubUsuario AxelBladelove -GitNombre AxelBladelove -GitCorreo AxelBladelove@users.noreply.github.com
```

Usa `-SinOnboarding` solo para automatizar. Para estudiantes nuevos, el flujo
normal recomendado es el asistente interactivo:

```bat
setup\instalar.cmd
```

## Archivos

| Archivo | Rol |
|---|---|
| `instalar.cmd` | Entrada doble-clickable para Windows |
| `instalar.ps1` | Orquestador principal |
| `utilidades.ps1` | Logs, consola, PATH y comandos |
| `herramientas.ps1` | Deteccion e instalacion con winget |
| `gcc_msys2.ps1` | Instalacion de GCC via MSYS2 |
| `vscode.ps1` | Terminal, extensiones, extension local y F9 |
| `proyecto.ps1` | Validacion del workspace, usuario y Git local |
