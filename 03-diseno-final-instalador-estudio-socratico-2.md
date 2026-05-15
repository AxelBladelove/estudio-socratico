# Diseño final del instalador de Estudio Socrático

## Decisión final

El instalador de Estudio Socrático debe evolucionar de un conjunto de scripts a una aplicación de instalación real:

```text
Estudio.Setup.exe
= .NET self-contained single-file
+ Terminal.Gui v2
+ motor idempotente de instalación
+ estado persistente
+ logs
+ modos instalar / actualizar / reparar / verificar
```

La interfaz debe ser visualmente agradable, navegable por teclado, con progreso claro, secciones por componente, acciones de reparación y reporte de errores. Sin embargo, la prioridad no debe ser solo que se vea bonito, sino que sea **idempotente, verificable y recuperable**.

Eso significa:

```text
puede ejecutarse varias veces
↓
no rompe lo que ya funciona
↓
repara lo que quedó incompleto
↓
verifica con pruebas reales
↓
guarda logs y estado
↓
permite actualizar sin perder el trabajo del usuario
```

## Objetivo de experiencia de usuario

El usuario debe poder instalar todo con una sola entrada:

```text
Doble clic en Install Estudio Socratico.cmd
```

O, si prefiere terminal:

```powershell
./setup/install.ps1
```

O desde Node, si ya tiene Node instalado:

```bash
npm run setup
```

Todas esas entradas deben terminar llamando al mismo motor:

```text
Estudio.Setup.exe
```

No deben existir cuatro instaladores distintos. Deben existir varias puertas de entrada hacia la misma aplicación.

## Estructura recomendada

```text
estudio-socratico/
├── setup/
│   ├── Install Estudio Socratico.cmd
│   ├── install.ps1
│   ├── Estudio.Setup.exe
│   └── Estudio.Setup/
│       ├── Program.cs
│       ├── Screens/
│       ├── Steps/
│       ├── Services/
│       └── State/
├── extension/
│   └── estudio-socratico-1.2.0.vsix
├── soporte/
│   ├── gists/
│   └── exercism/
└── package.json
```

## Papel de PowerShell 7

PowerShell 7 no será el framework visual del instalador.

El framework visual será:

```text
.NET + Terminal.Gui v2
```

PowerShell 7 será una herramienta que el instalador dejará instalada y configurada para el entorno de desarrollo del usuario.

Flujo:

```text
Windows PowerShell / cmd mínimo
↓
lanza Estudio.Setup.exe
↓
Estudio.Setup.exe instala PowerShell 7 si falta
↓
Estudio.Setup.exe configura PowerShell 7 como terminal predeterminada en VS Code
↓
Estudio.Setup.exe continúa sin que el usuario relance nada
```

PowerShell 7 servirá para:

```text
- futuras configuraciones;
- comandos de mantenimiento;
- scripts internos modernos;
- terminal predeterminada de VS Code;
- entorno limpio cuando el usuario formatea o estrena equipo.
```

## Motor de instalación por pasos

El instalador debe tener un motor interno basado en componentes. Cada componente debe implementar este contrato:

```text
Detect()
Install()
Update()
Repair()
Verify()
```

Ejemplo:

```text
Git
├── Detect: ¿git.exe existe? ¿git --version responde?
├── Install: instalar vía winget o fuente alternativa
├── Update: actualizar si hay versión nueva
├── Repair: corregir PATH o reinstalar
└── Verify: ejecutar comandos reales
```

Ningún paso debe marcarse como completado hasta pasar `Verify()`.

## Componentes que debe manejar

El instalador debe instalar, actualizar, reparar y verificar:

```text
- Git
- GitHub CLI
- Node.js LTS
- VS Code
- PowerShell 7
- Windows Terminal, si se decide recomendarlo
- MSYS2
- Pacman, como parte de MSYS2
- GCC / Make / GDB mediante MSYS2 UCRT64
- extensión VSIX de Estudio Socrático
- runtime config de Gemini
- catálogo/Gists internos de ejercicios
- perfil local del estudiante
- fork del usuario
- remotes Git
- settings de VS Code
```

## Aclaración sobre Pacman, MSYS2, GCC y Make

Pacman no debe tratarse como una herramienta separada que se instala antes de MSYS2.

La secuencia correcta es:

```text
1. Instalar MSYS2.
2. Verificar que existe C:\msys64\usr\bin\pacman.exe.
3. Usar pacman para instalar el toolchain UCRT64.
4. Verificar gcc, make/gdb y compilación real.
```

MSYS2 trae Pacman como su gestor de paquetes. Por tanto, el instalador no instala Pacman por separado: instala MSYS2 y luego verifica que Pacman quedó disponible.

Flujo técnico recomendado:

```text
winget install MSYS2.MSYS2
↓
verificar C:\msys64\usr\bin\pacman.exe
↓
ejecutar actualización inicial de MSYS2
↓
instalar toolchain UCRT64 con pacman
↓
agregar C:\msys64\ucrt64\bin al PATH si hace falta
↓
verificar gcc --version
↓
verificar mingw32-make --version
↓
compilar y ejecutar hello_world.c
```

Comandos conceptuales:

```bash
pacman -Syu --noconfirm
pacman -S --needed --noconfirm mingw-w64-ucrt-x86_64-toolchain
```

La verificación real debe incluir compilar un archivo C mínimo:

```c
#include <stdio.h>

int main(void) {
    printf("Estudio Socratico GCC OK\n");
    return 0;
}
```

Y luego ejecutar el `.exe` resultante. Si compila pero no ejecuta, no se considera instalación completamente válida.

## Interfaz visual

La TUI debe sentirse como una aplicación:

```text
┌──────────────────────────────────────────────────────────────┐
│ Estudio Socrático Setup                         v1.2.0       │
├───────────────────────┬──────────────────────────────────────┤
│ 1. Diagnóstico        │  Preparando entorno                   │
│ 2. Herramientas       │                                      │
│ 3. Compilador C       │  ✔ Git                               │
│ 4. VS Code            │  ✔ GitHub CLI                        │
│ 5. GitHub / Fork      │  → MSYS2 + GCC                       │
│ 6. Extensión          │  ○ VS Code                           │
│ 7. Gemini / Gists     │  ○ Extensión VSIX                    │
│ 8. Verificación       │                                      │
├───────────────────────┴──────────────────────────────────────┤
│ Detalle: instalando toolchain UCRT64...                      │
│ [██████████████████░░░░░░░░] 68%                              │
│                                                              │
│ [Ver log] [Reintentar paso] [Cancelar seguro]                 │
└──────────────────────────────────────────────────────────────┘
```

Debe tener tres niveles de lectura:

```text
Nivel 1: simple y bonito para usuario normal.
Nivel 2: detalle técnico expandible.
Nivel 3: log completo para copiar y depurar.
```

## Modos principales

El mismo ejecutable debe soportar:

```text
Instalar
Actualizar
Reparar
Verificar
```

### Instalar

Hace la configuración inicial completa.

```text
1. Diagnóstico del sistema.
2. Instalación de herramientas base.
3. Instalación de MSYS2 + GCC + Make.
4. Instalación de PowerShell 7.
5. Instalación/configuración de VS Code.
6. Instalación de extensión VSIX.
7. Configuración de Gemini.
8. Configuración de Gists/catálogo interno.
9. Configuración de GitHub/fork/alias.
10. Verificación final.
```

### Actualizar

Debe actualizar herramientas y proyecto sin perder el trabajo del usuario.

También debe permitir cambiar datos de identidad:

```text
- Cambiar cuenta de GitHub asociada.
- Cambiar alias local.
- Renombrar fork si el alias cambia.
- Crear fork nuevo si la cuenta de GitHub cambia.
- Reconfigurar remotes origin/upstream.
```

### Reparar

Debe corregir instalaciones incompletas o rotas:

```text
- arreglar PATH;
- reinstalar VSIX;
- reparar MSYS2/GCC;
- regenerar config local;
- revalidar Gemini;
- revalidar descarga de Gists;
- revalidar remotes Git;
- revalidar settings de VS Code.
```

### Verificar

Debe hacer pruebas sin modificar agresivamente el sistema.

```text
- git --version
- gh --version
- code --version
- pwsh --version
- gcc --version
- mingw32-make --version
- compilar hello.c
- listar extensiones de VS Code
- verificar config local
- probar descarga de un ejercicio Gist de prueba
```

## Estado persistente

El instalador debe guardar estado en:

```text
%LOCALAPPDATA%\EstudioSocratico\setup-state.json
%LOCALAPPDATA%\EstudioSocratico\logs\setup-YYYY-MM-DD.log
%APPDATA%\EstudioSocratico\config.json
```

Ejemplo:

```json
{
  "setupVersion": "1.2.0",
  "alias": "axel",
  "githubUser": "AxelBladelove",
  "forkOwner": "AxelBladelove",
  "forkName": "estudio-socratico-axel",
  "upstream": "AxelBladelove/estudio-socratico",
  "workspace": "C:\\Users\\Axel\\estudio-socratico",
  "installedComponents": {
    "git": "ok",
    "gh": "ok",
    "vscode": "ok",
    "powershell7": "ok",
    "msys2": "ok",
    "pacman": "ok",
    "gcc": "ok"
  },
  "lastSuccessfulStep": "verify-final"
}
```

## GitHub, alias y modelo de forks

El modelo final debe abandonar la idea de una rama por usuario dentro del repo principal.

Modelo recomendado:

```text
Repo principal:
AxelBladelove/estudio-socratico

Fork del usuario:
<github-user>/estudio-socratico-<alias>

Local:
origin   → fork del usuario
upstream → repo principal
```

### Aclaración conceptual

Un fork no es exactamente un worktree.

Un worktree es una copia de trabajo local adicional ligada al mismo repositorio Git local. Un fork es una copia remota del repositorio en otra cuenta de GitHub.

Pero para el objetivo del proyecto, sí se puede pensar en el fork como un espacio separado de trabajo:

```text
usuario trabaja en su fork
↓
no puede hacer merge directo al main principal
↓
si quiere aportar cambios, abre Pull Request
↓
el repo principal queda protegido
```

## Creación de fork

Durante instalación o actualización, si el usuario ya inició sesión con GitHub CLI, el instalador debe detectar su usuario:

```text
gh auth status
gh api user
```

Luego debe verificar si existe el fork esperado:

```text
<github-user>/estudio-socratico-<alias>
```

Si no existe, lo crea.

Comando conceptual:

```bash
gh repo fork AxelBladelove/estudio-socratico \
  --fork-name estudio-socratico-<alias> \
  --remote=true \
  --remote-name origin
```

Después configura:

```text
origin   = https://github.com/<github-user>/estudio-socratico-<alias>.git
upstream = https://github.com/AxelBladelove/estudio-socratico.git
```

## Cambio de cuenta de GitHub

El modo actualizar debe incluir una acción visible:

```text
Cambiar cuenta de GitHub asociada
```

Flujo:

```text
1. Guardar backup/autocommit local antes de tocar Git.
2. Cerrar o cambiar sesión de GitHub CLI si hace falta.
3. Ejecutar login de GitHub CLI con la nueva cuenta.
4. Leer nuevo githubUser.
5. Verificar si la nueva cuenta tiene fork con el alias actual.
6. Si no existe, crear fork nuevo en la nueva cuenta.
7. Cambiar origin al nuevo fork.
8. Mantener upstream apuntando al repo principal.
9. Hacer push del estado local al nuevo fork.
10. Actualizar setup-state.json.
```

Regla importante:

```text
Cambiar GitHub no renombra el fork anterior.
Crea o usa un fork en la nueva cuenta.
El fork viejo queda intacto salvo que el usuario pida archivarlo o eliminarlo.
```

## Cambio de alias

El modo actualizar también debe incluir:

```text
Cambiar alias local
```

El alias se usa para:

```text
- nombre del fork;
- carpeta del usuario;
- metadata local;
- configuración de la extensión;
- trazabilidad de ejercicios/respuestas.
```

Si cambia el alias de:

```text
axel
```

a:

```text
axelblade
```

entonces el nombre esperado del fork cambia de:

```text
estudio-socratico-axel
```

a:

```text
estudio-socratico-axelblade
```

Flujo:

```text
1. Validar alias nuevo.
2. Guardar backup/autocommit local.
3. Renombrar carpeta local del alias si aplica.
4. Renombrar metadata local.
5. Renombrar fork remoto con GitHub CLI.
6. Actualizar origin.
7. Actualizar settings de VS Code.
8. Actualizar config local.
9. Verificar push/pull.
```

Comando conceptual:

```bash
gh repo rename estudio-socratico-<nuevo-alias> \
  --repo <github-user>/estudio-socratico-<alias-anterior> \
  --yes
```

Luego:

```bash
git remote set-url origin https://github.com/<github-user>/estudio-socratico-<nuevo-alias>.git
```

## Validación de alias

El alias debe cumplir:

```text
- minúsculas recomendado;
- sin espacios;
- letras, números, guion y underscore;
- no empezar ni terminar con guion;
- no chocar con carpetas locales existentes;
- no chocar con repos remotos existentes del mismo usuario.
```

Si el alias nuevo ya existe como repo remoto del usuario, el instalador debe preguntar:

```text
Ya existe <github-user>/estudio-socratico-<alias>.
¿Quieres usar ese fork, elegir otro alias o cancelar?
```

## Actualización del proyecto con forks

Antes de actualizar el código del proyecto:

```text
1. git status
2. Si hay cambios, crear autocommit de seguridad o stash nombrado.
3. git fetch upstream
4. git merge upstream/main o git rebase upstream/main, según política final.
5. Resolver o reportar conflictos.
6. git push origin main
```

Recomendación para 1.2:

```text
merge upstream/main
```

Es más fácil de explicar y menos peligroso para usuarios novatos que un rebase.

## Política de protección de cambios del usuario

El instalador nunca debe borrar trabajo del usuario sin confirmación.

Antes de operaciones peligrosas:

```text
- crear backup;
- crear autocommit local;
- guardar log;
- permitir rollback razonable.
```

Commit de seguridad sugerido:

```text
chore(estudio): backup automático antes de actualizar
```

## VS Code y extensión

El instalador debe instalar el VSIX:

```bash
code --install-extension extension/estudio-socratico-1.2.0.vsix --force
```

Luego debe verificar:

```bash
code --list-extensions
```

Y confirmar que la extensión aparece instalada.

También debe escribir o actualizar settings de VS Code sin destruir la configuración previa del usuario.

Antes de tocar settings:

```text
1. leer settings.json actual;
2. hacer backup;
3. modificar solo las claves de Estudio Socrático;
4. preservar el resto.
```

Settings sugeridos:

```json
{
  "terminal.integrated.defaultProfile.windows": "PowerShell 7",
  "estudioSocratico.alias": "axel",
  "estudioSocratico.configPath": "%APPDATA%\\EstudioSocratico\\config.json"
}
```

## Gemini y Gists dentro del instalador

El instalador debe:

```text
1. Crear %APPDATA%\EstudioSocratico.
2. Obtener runtime config desde el VSIX/paquete.
3. Reconstruir o descargar Gemini config.
4. Guardar config local.
5. Verificar que la extensión puede leer config.json.
6. Verificar descarga de un Gist de ejercicio de prueba.
7. Guardar cache inicial si aplica.
```

Si Gemini falla:

```text
Instalación completa con advertencias.
Gemini no debe bloquear la descarga ni creación de ejercicios.
```

Si Gist falla:

```text
Instalación incompleta o completa con advertencia según criticidad.
Debe ofrecer Reintentar, Ver log y Continuar sin cache inicial.
```

## Actualizador de herramientas

El modo actualizar debe revisar versiones de:

```text
- Git
- GitHub CLI
- Node.js LTS
- VS Code
- PowerShell 7
- MSYS2
- paquetes UCRT64
- extensión VSIX
```

No debe ejecutar `winget upgrade --all` por defecto, porque eso podría actualizar programas ajenos al proyecto.

Debe actualizar solo dependencias controladas por Estudio Socrático.

## Reporte de errores

El instalador debe poder generar un reporte copiables:

```text
Estudio Socrático Setup Report
Fecha: 2026-05-11
Modo: Reparar
Usuario GitHub: AxelBladelove
Alias: axel
Fork: AxelBladelove/estudio-socratico-axel

Git: OK
GitHub CLI: OK
VS Code: OK
PowerShell 7: OK
MSYS2: OK
Pacman: OK
GCC: FALLÓ
Make: OK
VSIX: OK
Gemini: OK
Gists: OK

Error:
gcc.exe no encontrado en C:\msys64\ucrt64\bin

Acción sugerida:
Reinstalar toolchain UCRT64 y reparar PATH.
```

## Orden recomendado de implementación

No empezar por la TUI bonita. Primero construir el motor.

```text
1. Crear Estudio.Setup.exe básico.
2. Implementar sistema de pasos Detect/Install/Verify.
3. Implementar logs y setup-state.json.
4. Implementar Git, VS Code y PowerShell 7.
5. Implementar MSYS2/Pacman/GCC/Make con healthcheck real.
6. Implementar VSIX install/verify.
7. Implementar Gemini/Gists runtime.
8. Implementar GitHub auth + fork + alias.
9. Implementar modo update.
10. Implementar cambio de GitHub y cambio de alias.
11. Implementar modo repair.
12. Embellecer la TUI.
```

## Decisión cerrada

Para la versión 1.2/2.0, el instalador debe quedar así:

```text
✔ Un solo motor: Estudio.Setup.exe.
✔ Varias entradas: cmd, ps1, npm.
✔ .NET self-contained + Terminal.Gui v2.
✔ PowerShell 7 instalado/configurado, no usado como TUI.
✔ MSYS2 instala Pacman; Pacman instala GCC/Make/GDB.
✔ Verificación real compilando C.
✔ Modo instalar, actualizar, reparar y verificar.
✔ Actualizador selectivo de herramientas.
✔ Modelo de forks por usuario/alias.
✔ Cambio de GitHub crea o usa fork nuevo.
✔ Cambio de alias renombra fork y metadata local.
✔ origin apunta al fork; upstream apunta al repo principal.
✔ El usuario no hace merge directo al main principal; contribuye por Pull Request.
✔ Gemini/Gists integrados al setup sin configuración manual.
✔ Logs, reportes y estado persistente.
```
