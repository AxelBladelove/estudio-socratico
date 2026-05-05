# Sistema de Estudio Socratico

Framework local para estudiar Fundamentos de Programacion en C con telemetria,
logs por bloque, base de errores acumulativa y protocolos de IA tipo tutor
socratico.

El proyecto esta pensado para usarse en Windows con VS Code o Antigravity,
`gcc` y Git. Cada compilacion puede dejar rastro util para revisar despues que
hiciste, que fallo y que modelo mental se repite.

La identidad Git local del clon debe salir de tu usuario de GitHub. El setup
usa `git config --local github.user` como fuente principal para `github.user`,
`user.name` y, si hace falta, `user.email`.

## Que hace este sistema

- Compila el archivo `.c` activo con `Ctrl+Shift+B`.
- Guarda telemetria por usuario en `usuarios/<usuario>/...`.
- Organiza las sesiones en bloques de 45 minutos.
- Hace commits automaticos etiquetados con usuario, timestamp, duracion y exit code.
- Permite dos protocolos de IA:
  - `@revisar`: una pista socratica breve, sin codigo.
  - `@sintetizar`: cierre de sesion y actualizacion de la base de errores.

## Conceptos Clave

### Identidad Local: `.estudio_usuario`

Cada clon local del repo debe tener un archivo `.estudio_usuario` con una sola
linea: tu slug personal.

Ejemplos:

```text
axel
eric
```

Ese slug determina estas rutas:

- `usuarios/<slug>/errores.md`
- `usuarios/<slug>/logs/<ejercicio>/bloqueN.log`

El archivo `.estudio_usuario` esta ignorado por Git: cada persona tiene el suyo.

Si no existe, `soporte/scripts/compilar_y_grabar.bat` intenta derivarlo en este orden:

1. `ESTUDIO_USUARIO`
2. `git config github.user`
3. `git config user.name`
4. usuario de Windows

Tambien crea `.estudio_usuario` automaticamente si hacia falta.

### Telemetria Por Usuario

El modelo actual del sistema es **usuario-centrico**. La telemetria primaria ya
no vive en la raiz del repo, sino aqui:

```text
usuarios/<usuario>/errores.md
usuarios/<usuario>/logs/<ejercicio>/bloque1.log
usuarios/<usuario>/logs/<ejercicio>/bloque2.log
usuarios/<usuario>/logs/<ejercicio>/bloqueN.log
```

Compatibilidad:

- `errores.md` y `logs/` en la raiz se conservan como legado durante la migracion.
- El script nuevo prioriza `usuarios/<usuario>/...`.
- Si todavia no existe la telemetria por usuario, usa el legado como fuente
  inicial cuando corresponde.

### Bloques De 45 Minutos

Las sesiones se agrupan automaticamente en bloques de 45 minutos.

Ejemplo:

```text
usuarios/axel/logs/Blackjack/bloque1.log
usuarios/axel/logs/Blackjack/bloque2.log
```

El archivo interno `bloque_actual.txt` guarda el numero de bloque activo y el
timestamp de inicio del bloque. Ese archivo es interno y esta ignorado por Git.

### Commits Automaticos

Despues de cada compilacion, el sistema intenta crear un commit automatico con
este formato:

```text
intento_<usuario>_YYYY-MM-DDTHH-mm-ss_<duracion>_exitN
```

Ejemplo:

```text
intento_axel_2026-05-02T18-42-11_01h28m_exit0
```

Significado:

- `<usuario>`: slug local del clon.
- `YYYY-MM-DDTHH-mm-ss`: hora real del intento.
- `<duracion>`: tiempo acumulado del ejercicio desde el primer bloque detectado.
- `exitN`: resultado de compilacion. `exit0` significa compilacion exitosa.

El commit automatico no hace `git add -A` sobre todo el repo. Solo intenta
agregar:

- el archivo `.c` compilado
- el log del ejercicio actual
- el `errores.md` del usuario actual

## Instalacion Rapida

### Opcion 1: Windows Nativo

Entrada recomendada:

```bat
setup\instalar.cmd
```

Tambien puedes abrir la carpeta del proyecto y ejecutar:

```bat
winsetup
```

Si abriste PowerShell por primera vez en esa carpeta:

```powershell
cmd /c winsetup
```

### Opcion 2: Scripts npm/pnpm/bun

```bash
npm run setup
```

```bash
pnpm run setup
```

```bash
bun run setup
```

### Opcion 3: Verificacion Sin Cambios

```bash
npm run setup:dry
```

o:

```bat
setup\instalar.cmd -SoloVerificar -SinWinget -SinExtensiones
```

## Setup Manual

### 1. Requisitos

- `git` instalado.
- `gcc` disponible en el `PATH` o en `C:\msys64\mingw64\bin`.
- La carpeta `estudio-socratico/` abierta como workspace en VS Code o editor compatible con tareas.

### 2. Inicializar Git

```bash
git init
git config user.email "tu-correo@users.noreply.github.com"
git config user.name "tu-usuario-de-github"
git config github.user "tu-usuario-de-github"
git add .
git commit -m "setup_inicial"
```

### 3. Crear Tu Identidad Local Del Clon

```text
copy .estudio_usuario.example .estudio_usuario
```

Luego edita `.estudio_usuario` y deja una sola linea con tu slug.

Ejemplo:

```text
axel
```

### 4. Compilar Con `Ctrl+Shift+B`

La tarea ya esta definida en `.vscode/tasks.json`.

### Compatibilidad Con `conio.h`

El repo incluye una cabecera local en `include/conio.h` para ejercicios que
pidan funciones clasicas de consola como:

- `gotoxy(x, y)`
- `clrscr()`
- `getch()`, `getche()`, `kbhit()`
- `textcolor()`, `textbackground()`, `normvideo()`

En tus ejercicios puedes escribir:

```c
#include <conio.h>
```

Luego compila normalmente con `Ctrl+Shift+B`. El script ya pasa la carpeta
`include/` a `gcc`, asi que no hace falta copiar librerias ni cambiar el
comando de compilacion.

## Flujo Diario

### Al comenzar un ejercicio

Crea un archivo `.c` dentro de `Ejercicios/`. La primera linea debe ser el
Contrato Logico: el enunciado del ejercicio como comentario multilinea.

Ejemplo:

```c
/* Ejercicio: Leer N numeros enteros usando memoria dinamica,
   e imprimir la suma de los que sean primos. */

#include <stdio.h>
#include <stdlib.h>
```

### Durante la codificacion

1. Programa normalmente.
2. Presiona `Ctrl+Shift+B` para compilar.
3. El sistema:
   - compila con `gcc`
   - abre el ejecutable en una ventana aparte si compilo
   - guarda log en `usuarios/<usuario>/logs/<ejercicio>/bloqueN.log`
   - asegura que exista `usuarios/<usuario>/errores.md`
   - hace un commit automatico si hay cambios rastreables

### Si te atascas

Escribe:

```text
@revisar
```

La IA debe leer:

- `AGENTS.md`
- `.agent/skills/revisar/SKILL.md`
- `.estudio_usuario`
- tu archivo `.c`
- `usuarios/<slug>/errores.md`
- y, si hace falta, el bloque mas reciente del ejercicio actual

La respuesta debe ser una pista socratica breve, sin codigo C, sin nombres de
variables, sin linea exacta.

### Al final del bloque de estudio

Escribe:

```text
@sintetizar
```

La IA debe:

- leer `AGENTS.md`
- leer `.agent/skills/sintetizar/SKILL.md`
- resolver el slug actual desde `.estudio_usuario`
- analizar git log y logs del ejercicio
- actualizar `usuarios/<slug>/errores.md`

## Git Y Colaboracion

### Esquema recomendado de ramas

- `main`: base estable del sistema
- `axel`: trabajo personal de Axel
- `eric`: trabajo personal de Eric
- `coop`: trabajo conjunto temporal

### Flujo minimo sugerido

Crear tu rama personal desde `main`:

```bash
git switch main
git pull origin main
git switch -c axel
```

Subirla por primera vez:

```bash
git push -u origin axel
```

Traer cambios nuevos de la base a tu rama:

```bash
git switch axel
git fetch origin
git merge origin/main
```

Crear una rama conjunta:

```bash
git switch axel
git pull
git switch -c coop
git push -u origin coop
```

### Comandos Git basicos y que hace cada uno

Ver estado actual:

```bash
git status
```

Ver ramas locales:

```bash
git branch
```

Ver ramas locales y remotas:

```bash
git branch -a
```

Cambiar de rama:

```bash
git switch main
```

Crear rama y cambiarte a ella:

```bash
git switch -c axel
```

Crear rama sin cambiarte:

```bash
git branch coop
```

Añadir todo lo modificado del directorio actual:

```bash
git add .
```

Añadir un archivo concreto:

```bash
git add soporte/scripts/compilar_y_grabar.bat
```

Hacer commit manual:

```bash
git commit -m "mensaje"
```

Descargar cambios remotos sin mezclarlos:

```bash
git fetch origin
```

Descargar y mezclar directamente:

```bash
git pull
```

Subir cambios al remoto:

```bash
git push
```

Subir una rama por primera vez y enlazarla:

```bash
git push -u origin axel
```

Renombrar la rama actual:

```bash
git branch -m axel
```

Renombrar otra rama:

```bash
git branch -m axel-vieja axel
```

Borrar una rama local:

```bash
git branch -d coop
```

Borrar una rama remota:

```bash
git push origin --delete coop
```

### Remotos

Un remoto es la copia del repo en otro lugar, normalmente GitHub.

Ejemplo:

```bash
git remote -v
```

Normalmente veras `origin` como nombre del remoto principal.

## Estructura Del Proyecto

```text
estudio-socratico/
|-- .agent/
|   |-- skills/
|   |   |-- revisar/SKILL.md
|   |   `-- sintetizar/SKILL.md
|-- .vscode/
|   |-- codex-instructions.md
|   `-- tasks.json
|-- soporte/
|   |-- consola/
|   |   |-- conio.c
|   |   |-- console_cp437.h
|   |   |-- sys_dump_console.c
|   |   `-- wait_any_key.c
|   `-- runtime/        (generado localmente; ignorado por git)
|-- setup/
|   |-- instalar.cmd
|   |-- instalar.ps1
|   |-- herramientas.ps1
|   |-- gcc_msys2.ps1
|   |-- proyecto.ps1
|   |-- utilidades.ps1
|   |-- vscode.ps1
|   `-- README.md
|-- usuarios/
|   |-- README.md
|   `-- <usuario>/
|       |-- errores.md
|       `-- logs/
|           `-- <ejercicio>/
|               |-- bloque1.log
|               |-- bloque2.log
|               `-- bloque_actual.txt
|-- Ejercicios/
|-- AGENTS.md
|-- errores.template.md
|-- errores.md                        legado durante migracion
|-- logs/                             legado durante migracion
|-- .estudio_usuario.example
|-- package.json
|-- README.md
`-- soporte/
  |-- consola/
  |   |-- conio.c
  |   |-- console_cp437.h
  |   |-- sys_dump_console.c
  |   `-- wait_any_key.c
  |-- runtime/                      generado localmente; ignorado por git
  `-- scripts/
    |-- build.cmd
    `-- compilar_y_grabar.bat
```

## Migracion Desde El Modelo Legado

Si vienes del modelo viejo, es normal que ya tengas estos archivos en la raiz:

- `errores.md`
- `logs/`

No hace falta borrarlos de inmediato. El sistema nuevo ya prioriza
`usuarios/<usuario>/...` y usa el legado solo como compatibilidad.

### Migracion suave

1. Crea tu identidad local:

```text
copy .estudio_usuario.example .estudio_usuario
```

2. Escribe tu slug en `.estudio_usuario`, por ejemplo:

```text
axel
```

3. Compila una vez con `Ctrl+Shift+B`.

Eso hara que el sistema cree automaticamente:

- `usuarios/axel/errores.md`
- `usuarios/axel/logs/<ejercicio>/...`

Si `usuarios/axel/errores.md` no existe todavia, el script lo inicializa desde
`errores.md` legado o desde `errores.template.md`.

### Que pasa con los logs viejos

Los logs antiguos en `logs/` no se mueven solos. Se quedan como historial legado.
Los intentos nuevos ya caen en `usuarios/<usuario>/logs/`.

Si quieres migrarlos manualmente mas adelante, puedes copiar la carpeta del
ejercicio viejo a tu carpeta de usuario correspondiente, pero no es obligatorio
para empezar a usar el sistema nuevo.

## Archivos Ignorados Por Git

- `.estudio_usuario`
- `*.exe`
- `logs/<ejercicio>/bloque_actual.txt`
- `usuarios/<usuario>/logs/<ejercicio>/bloque_actual.txt`
- archivos temporales del compilador

## Archivos Rastreados Por Git

- `usuarios/<usuario>/logs/<ejercicio>/bloqueN.log`
- `usuarios/<usuario>/errores.md`
- scripts del sistema
- skills
- tareas de VS Code
- documentacion

## IA Soportada

- Codex en VS Code: lee `AGENTS.md` y `.vscode/codex-instructions.md`
- Antigravity/OpenCode y clientes con skills: usan `.agent/skills/revisar/SKILL.md` y `.agent/skills/sintetizar/SKILL.md`
- Otros clientes: pueden trabajar si pueden leer el workspace y seguir `AGENTS.md`

## Troubleshooting

**El script no se ejecuta al presionar `Ctrl+Shift+B`:**
verifica que el workspace abierto sea la carpeta `estudio-socratico/`.

**`gcc` no se encuentra:**
instala MSYS2/MinGW y agrega `C:\msys64\mingw64\bin` o tu ruta equivalente al
`PATH`.

**El git commit falla silenciosamente:**
ejecuta `git init` y configura `user.email`, `user.name` y, si quieres,
`github.user`. Si quieres separar bien tu telemetria, crea `.estudio_usuario`
antes de compilar.

**La IA no responde a `@sintetizar` o `@revisar`:**
usa la frase:

```text
Lee AGENTS.md y ejecuta el protocolo revisar/sintetizar.
```

**Tengo `errores.md` y `logs/` en la raiz:**
se conservan como legado durante la migracion. El sistema nuevo ya prioriza
`usuarios/<usuario>/...`.
