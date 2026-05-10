# Guia Tecnica Del Framework

Esta guia es para quien quiera mantener, extender o depurar Estudio Socratico.
El README principal esta escrito para estudiantes; este archivo explica el
motor.

## Cambios Clave En 1.2

- `npm run setup` ejecuta la TUI completa y `npm run setup:update` valida una
  instalacion existente sin repetir identidad si ya esta configurada.
- La identidad del estudiante se resuelve como alias local vinculado a
  `gh auth`; el alias se usa para rama, `.estudio_usuario` y commits.
- El panel de ejercicios usa `soporte/exercism/manager.ps1` para catalogo,
  importacion, tests Exercism, submit y validacion local.
- Los ejercicios no-Exercism pueden tener tests generados por IA en
  `.estudio-tests/`; la extension solo marca completado cuando `validate`
  devuelve exit code 0.
- W3Schools/w3resource y PDF Alejandro se exponen como catalogos estaticos con
  metadata ligera y `driveFileId`. Los enunciados completos viven en Google
  Drive; el texto local solo puede existir en `.estudio-drive/`, que no se sube
  a Git.

## Resumen De Arquitectura

El flujo principal es:

1. VS Code ejecuta la tarea `Compilar y Grabar (Sistema Socratico)`.
2. La tarea llama a `soporte/scripts/build.cmd`.
3. `build.cmd` llama a `soporte/scripts/compilar_y_grabar.bat`.
4. El script resuelve usuario, bloque, rutas, GCC, runtime y log.
5. Compila el `.c` activo con `gcc`.
6. Si compila, abre una consola externa estilo Code::Blocks usando
   `soporte/runtime/_output.exe`.
7. Al cerrar la consola, `finalizar_intento.bat` agrega el `.c`, el log y
   `usuarios/<slug>/errores.md` al commit automatico.

## Archivos Principales

| Archivo | Rol |
|---|---|
| `.vscode/tasks.json` | Define tareas de build normal y Live Share |
| `soporte/scripts/build.cmd` | Entrada ligera llamada por VS Code |
| `soporte/scripts/compilar_y_grabar.bat` | Orquestador de compilacion, logs y runtime |
| `soporte/scripts/resolve_build_context.ps1` | Resuelve usuario, bloque y rebuilds |
| `soporte/scripts/finalizar_intento.bat` | Hace `git add` y commit automatico |
| `soporte/consola/output_launcher.c` | Consola externa estilo Code::Blocks |
| `soporte/consola/conio.c` | Implementacion local de funciones de consola |
| `include/conio.h` | Cabecera usada por ejercicios C |
| `include/estudio_stdio_cp437.h` | Compatibilidad de `printf` con simbolos CP437 |
| `setup/instalar.ps1` | Orquestador del setup |
| `setup/proyecto.ps1` | Validacion del repo, onboarding y Git local |
| `setup/vscode.ps1` | F9, extensiones y terminal |
| `soporte/exercism/manager.ps1` | Backend para catalogo, import, test y submit de ejercicios externos |
| `soporte/vscode/estudio-exercism/` | Extension local de VS Code |

## Identidad Del Usuario

El archivo local `.estudio_usuario` contiene un slug simple:

```text
axel
```

Ese slug decide:

```text
usuarios/axel/errores.md
usuarios/axel/logs/<ejercicio>/bloqueN.log
```

`.estudio_usuario` esta ignorado por Git porque cada clon puede pertenecer a una
persona distinta.

Fallbacks si falta `.estudio_usuario`:

1. `ESTUDIO_USUARIO`
2. `git config --local github.user`
3. `git config --local user.name`
4. usuario de Windows

## Setup 1.0

Entrada recomendada:

```bat
setup\instalar.cmd
```

El setup:

- valida la raiz del proyecto;
- pide datos de onboarding si faltan;
- instala o valida herramientas base;
- instala o valida Exercism CLI;
- configura Git local;
- escribe `.estudio_usuario`;
- crea `usuarios/<slug>/errores.md` vacio si falta;
- crea o cambia a la rama personal cuando es posible;
- valida GCC/MSYS2;
- instala `make` y `mingw32-make` en MSYS2 para tests de Exercism C;
- compila `soporte/runtime/_output.exe`;
- compila `soporte/runtime/conio_support.o`;
- configura `F9` en VS Code.
- empaca e instala la extension local de ejercicios.

Parametros utiles:

| Parametro | Uso |
|---|---|
| `-SoloVerificar` | Muestra que haria sin modificar sistema |
| `-SinWinget` | No instala herramientas automaticamente |
| `-SinExtensiones` | No instala extensiones de VS Code |
| `-SinOnboarding` | Usa argumentos/configuracion sin preguntar |
| `-SinRamaUsuario` | No crea ni cambia rama personal |
| `-UsuarioSlug <slug>` | Define slug local |
| `-GitHubUsuario <usuario>` | Define `github.user` |
| `-GitNombre <nombre>` | Define `user.name` |
| `-GitCorreo <correo>` | Define `user.email` |

Ejemplo no interactivo:

```powershell
setup\instalar.cmd -SinOnboarding -UsuarioSlug axel -GitHubUsuario AxelBladelove -GitNombre AxelBladelove -GitCorreo AxelBladelove@users.noreply.github.com
```

## Build Normal

La tarea normal usa:

```text
Compilar y Grabar (Sistema Socratico)
```

Comportamiento:

- compila el archivo `.c` activo;
- antes de compilar, detecta si el archivo pertenece a un ejercicio de
  Exercism importado;
- si detecta Exercism, ejecuta `soporte/exercism/manager.ps1 -Action test`;
- abre el ejecutable en una ventana externa;
- bloquea ejecuciones duplicadas mediante `soporte/runtime/run.lock`;
- difiere el commit automatico hasta que el estudiante cierre la ventana;
- guarda salida del programa en el log cuando el launcher recibe `--log`.

## Integracion Exercism

El backend vive en:

```text
soporte/exercism/manager.ps1
```

Acciones principales:

| Accion | Uso |
|---|---|
| `status` | valida CLI, workspace y variables auxiliares |
| `catalog` | devuelve JSON para la extension |
| `import` | descarga/copia un ejercicio y crea metadata local |
| `detect` | usado por F9 para saber si debe correr tests |
| `test` | ejecuta `exercism test`, guarda log y commit |
| `submit` | ejecuta `exercism submit` desde la carpeta importada |

La extension de VS Code llama al mismo backend, asi el comportamiento es igual
desde botones, terminal o `F9`.

### Catalogo

Exercism usa el endpoint publico:

```text
https://api.exercism.org/v2/tracks/c/exercises
```

Ese endpoint devuelve los ejercicios del track C en el orden que usa la pagina.
Si no hay internet, el backend usa una lista minima de fallback.

Los proveedores no remotos viven en:

```text
soporte/exercism/catalogs/w3schools.json
soporte/exercism/catalogs/alejandro.json
```

Ambos se muestran en la misma UI. El catalogo versionado no debe guardar el
enunciado completo del ejercicio: solo metadata para pintar tarjetas y el
`driveFileId` publico usado al importar.

### Google Drive De Mantenimiento

Drive se usa solo para publicar paquetes de ejercicios. Los estudiantes no
necesitan OAuth ni token de Drive: la extension descarga por `driveFileId`
publico.

Comandos de mantenedor:

```bat
npm run drive:auth
npm run drive:check
npm run drive:generate
npm run drive:sync
```

Archivos locales ignorados por Git:

```text
.estudio-drive/oauth-client.json
.estudio-drive/token.json
.estudio-drive/source/
.estudio-drive/generated/
```

`drive:sync` sube/actualiza Markdown en Drive, marca cada archivo como publico
de solo lectura y escribe el `driveFileId` en el catalogo. Por defecto elimina
`instructionMarkdown` del catalogo versionado despues de subirlo. Usa
`--keep-local-text` solo durante depuracion.

### Estructura De Un Ejercicio Importado

Ejemplo:

```text
Ejercicios/Grains/
  .estudio-exercism/
    support/
      .exercism/
      README.md
      makefile
      test_grains.c
      test-framework/
  .estudio-exercism.json
  grains.c
  grains.h
```

El Explorer de VS Code oculta `.estudio-exercism/` y `.estudio-exercism.json`
para que el estudiante vea principalmente los archivos de solucion (`.c` y `.h`).
El backend sincroniza esos archivos hacia `support/` antes de correr `test` o
`submit`.

`.estudio-exercism.json` guarda proveedor, slug, titulo, estado local, carpeta
interna y archivos de solucion. No guarda tokens.

### Tokens

Exercism usa la configuracion global del CLI en la PC del estudiante:

```bat
exercism configure --token TU_TOKEN
```

El proyecto no guarda ese token en Git ni en `usuarios/`. El setup solo valida
si existe y muestra una instruccion si falta.

Para traducciones automaticas se lee primero la clave compartida del repo en
`soporte/exercism/config.json`, luego `soporte/exercism/config.local.json`,
despues `.estudio_exercism.local.json` y por ultimo `GEMINI_API_KEY` desde el
entorno. Si no existe ninguna, el import deja un README con traduccion pendiente.

### Extension Local

Fuente:

```text
soporte/vscode/estudio-exercism/
```

Durante setup, `setup/vscode.ps1` ejecuta:

1. `npm install` dentro de la extension;
2. `npx vsce package --no-dependencies --allow-missing-repository`;
3. `code --install-extension soporte/runtime/vscode/estudio-exercism.vsix --force`.

La extension aporta comandos:

```text
Estudio Socratico: Abrir Panel de Ejercicios
Estudio Socratico: Probar Ejercicio Actual
Estudio Socratico: Enviar Ejercicio Actual
```

## Build Para Live Share

La tarea:

```text
Compilar y Grabar (Live Share Terminal)
```

pasa `--inline` al script. Eso ejecuta el programa en la terminal compartida en
lugar de abrir una ventana externa.

## Logs

Los logs por usuario son la telemetria principal:

```text
usuarios/<slug>/logs/<ejercicio>/bloqueN.log
```

Cada log contiene:

- separador de intento;
- timestamp;
- ruta del archivo;
- copia del codigo fuente;
- salida del compilador;
- exit code;
- salida del programa si se ejecuto mediante launcher con `--log`.

`bloque_actual.txt` guarda estado interno del bloque y esta ignorado por Git.

## Errores.md

`usuarios/<slug>/errores.md` empieza vacio en la version 1.0. La skill
`@sintetizar` puede actualizarlo al final de una sesion para convertirlo en una
memoria de patrones.

No debe llenarse con logs crudos. Debe documentar modelos mentales:

- concepto;
- patron observado;
- como reconocerlo;
- pista de estudio;
- frecuencia o evidencia.

## Compatibilidad De Consola Y Conio

El objetivo es que los ejercicios que piden `conio.h` se comporten como en
Code::Blocks dentro de este repo.

Piezas importantes:

- `include/conio.h` expone las funciones usadas por ejercicios.
- `soporte/consola/conio.c` implementa funciones sobre la consola de Windows.
- `include/estudio_stdio_cp437.h` envuelve salida estandar para que codigos
  CP437 clasicos como cartas, corazones y bordes se escriban correctamente.
- `output_launcher.c` crea una experiencia de ejecucion externa con mensaje
  `Process returned ... execution time ...`.

Si algo visual se rompe, separa tres causas:

1. el programa imprime mas columnas de las que cree;
2. el caracter no pertenece al mismo encoding;
3. el framework no esta traduciendo o posicionando como Code::Blocks.

## Skills

### `@revisar`

Archivo:

```text
.agent/skills/revisar/SKILL.md
```

Debe reunir contexto por su cuenta y responder como tutor. Puede ser concreto,
pero no debe entregar el codigo final.

### `@ver`

Archivo:

```text
.agent/skills/ver/SKILL.md
```

Hace prueba de escritorio RAM sobre una funcion, `main`, un ciclo o todo el
archivo segun la linea indicada.

### `@sintetizar`

Archivo:

```text
.agent/skills/sintetizar/SKILL.md
```

Resume la sesion y actualiza `errores.md`.

## Ramas

Modelo recomendado:

| Rama | Rol |
|---|---|
| `main` | Framework estable |
| `pair` | Trabajo compartido |
| `<slug>` | Trabajo personal |

Para publicar una version del framework:

1. prepara cambios en una rama limpia;
2. verifica setup, build y docs;
3. mergea o empuja a `main`;
4. actualiza `pair` y ramas personales que deban heredar el framework.

## Release 1.0

La release 1.0 debe quedar limpia:

- un solo `.c` de ejemplo: `Ejercicios/Blackjack.c`;
- sin logs historicos versionados;
- `usuarios/*/errores.md` vacios;
- runtime generado ignorado por Git;
- setup capaz de preparar una maquina nueva;
- README principal para estudiantes;
- esta guia para mantenedores.
