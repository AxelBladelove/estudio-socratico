# Guia Tecnica Del Framework

Esta guia es para quien quiera mantener, extender o depurar Estudio Socratico.
El README principal esta escrito para estudiantes; este archivo explica el
motor.

## Cambios Clave En 2.0

- El setup anterior en `_estudio/setup` fue eliminado. No se usa como base ni
  como referencia para la version 2.0.
- El nuevo configurador vive en `_estudio/installer` y se empaqueta como `.exe`
  WiX Burn para GitHub Releases.
- La experiencia principal es WinUI 3 self-contained sobre .NET 10 LTS; el motor
  real esta en librerias C# y las tareas admin viven en un worker elevado.
- El instalador tiene modos de instalacion, reparacion, reinstalacion y
  desinstalacion, con logs persistentes y diagnostico copiable.
- WinGet instala herramientas base; MSYS2/pacman instala GCC, MinGW-w64 y Make.
- La identidad del estudiante se resuelve como alias local vinculado a
  `gh auth`; Git local toma `github.user` y `user.email` desde la cuenta
  autenticada, mientras el alias se usa para `user.name`, rama y
  `.estudio_usuario`.
- El panel de ejercicios usa `_estudio/soporte/exercism/manager.ps1` para catalogo,
  importacion, tests Exercism, submit y validacion local.
- Los ejercicios no-Exercism pueden tener tests generados por IA en
  `.estudio-tests/`; la extension solo marca completado cuando `validate`
  devuelve exit code 0.
- PDF Alejandro se expone como catalogo estatico con metadata ligera y URLs de
  Gist. Los enunciados completos se descargan desde Gist al importar; no deben
  empaquetarse dentro del VSIX.
- W3Schools / w3resource fue retirado en la version 1.2. Se planea
  reintroducirlo desde cero en una version futura con curaduria semantica.

## Resumen De Arquitectura

El flujo principal es:

1. VS Code ejecuta la tarea `Compilar y Grabar (Sistema Socratico)`.
2. La tarea llama a `_estudio/soporte/scripts/build.cmd`.
3. `build.cmd` llama a `_estudio/soporte/scripts/compilar_y_grabar.bat`.
4. El script resuelve usuario, bloque, rutas, GCC, runtime y log.
5. Compila el `.c` activo con `gcc`.
6. Si compila, abre una consola externa estilo Code::Blocks usando
   `_estudio/soporte/runtime/_output.exe`.
7. Al cerrar la consola, `finalizar_intento.bat` agrega el `.c`, el log y
   `usuario/errores.md` al commit automatico.

## Archivos Principales

| Archivo | Rol |
|---|---|
| `.vscode/tasks.json` | Define tareas de build normal y Live Share |
| `_estudio/soporte/scripts/build.cmd` | Entrada ligera llamada por VS Code |
| `_estudio/soporte/scripts/compilar_y_grabar.bat` | Orquestador de compilacion, logs y runtime |
| `_estudio/soporte/scripts/resolve_build_context.ps1` | Resuelve usuario, bloque y rebuilds |
| `_estudio/soporte/scripts/finalizar_intento.bat` | Hace `git add` y commit automatico |
| `_estudio/soporte/consola/output_launcher.c` | Consola externa estilo Code::Blocks |
| `_estudio/soporte/consola/conio.c` | Implementacion local de funciones de consola |
| `_estudio/include/conio.h` | Cabecera usada por ejercicios C |
| `_estudio/include/estudio_stdio_cp437.h` | Compatibilidad de `printf` con simbolos CP437 |
| `_estudio/installer/` | Fuente del instalador/configurador v2 |
| `_estudio/installer/src/EstudioSocratico.Configurator.Engine/ConfiguratorEngine.cs` | Orquestador de instalacion, reparacion, reinstalacion y uninstall |
| `_estudio/installer/src/EstudioSocratico.Configurator.Engine/DependencyInstallation.cs` | Deteccion, WinGet, fallback oficial, MSYS2/GCC/Make |
| `_estudio/installer/src/EstudioSocratico.Configurator.Engine/ProductManagers.cs` | GitHub, Exercism, VS Code, workspace, Gist y telemetria |
| `_estudio/installer/src/EstudioSocratico.Configurator.App/MainWindow.xaml` | UI WinUI 3 principal |
| `_estudio/installer/src/EstudioSocratico.Configurator.Elevated/Program.cs` | Worker elevado para operaciones admin concretas |
| `_estudio/soporte/exercism/manager.ps1` | Backend para catalogo, import, test y submit de ejercicios externos |
| `_estudio/soporte/vscode/estudio-exercism/` | Extension local de VS Code |

## Identidad Del Usuario

El archivo local `.estudio_usuario` contiene un slug simple:

```text
axel
```

Ese slug decide:

```text
usuario/errores.md
usuario/logs/<ejercicio>/bloqueN.log
```

`.estudio_usuario` esta ignorado por Git porque cada clon puede pertenecer a una
persona distinta.

Fallbacks si falta `.estudio_usuario`:

1. `ESTUDIO_USUARIO`
2. `git config --local github.user`
3. `git config --local user.name`
4. usuario de Windows

## Instalador 2.0

Entrada para usuarios finales:

```text
Estudio-Socratico-Setup-v2.0.0-x64.exe
```

Fuente:

```text
_estudio/installer/
```

Comandos de mantenedor:

```powershell
dotnet restore _estudio/installer/EstudioSocratico.Installer.sln
npm run installer:test
npm run installer:build
```

El instalador:

- detecta Node.js, Python, Git, GitHub CLI, Exercism CLI, VS Code, MSYS2,
  GCC/MinGW-w64 y Make;
- instala dependencias faltantes con WinGet y paquetes MSYS2 con pacman;
- valida cada herramienta con comandos reales;
- autentica GitHub con `gh auth login --web` y configura Git con
  `gh auth setup-git`;
- crea o reutiliza el fork del usuario y configura `origin`/`upstream`;
- escribe `.estudio_usuario`, `usuario/errores.md` y la rama personal;
- configura Exercism CLI con token sin loguearlo;
- instala extensiones recomendadas, copia la extension local al perfil dedicado
  de VS Code y configura F9;
- compila `_estudio/soporte/runtime/_output.exe` y `conio_support.o`;
- guarda logs en `%LocalAppData%\EstudioSocratico\Logs` y redacta secretos.

La decision tecnica y fuentes consultadas estan en:

```text
_estudio/installer/docs/arquitectura.md
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
- si detecta Exercism, ejecuta `_estudio/soporte/exercism/manager.ps1 -Action test`;
- para Exercism, el runner local copia el soporte a un workspace temporal y comenta los `TEST_IGNORE()` oficiales para que el resultado local refleje toda la suite, no solo el modo incremental;
- abre el ejecutable en una ventana externa;
- bloquea ejecuciones duplicadas mediante `_estudio/soporte/runtime/run.lock`;
- difiere el commit automatico hasta que el estudiante cierre la ventana;
- guarda salida del programa en el log cuando el launcher recibe `--log`.

## Integracion Exercism

El backend vive en:

```text
_estudio/soporte/exercism/manager.ps1
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

Los proveedores no remotos vigentes viven en:

```text
_estudio/soporte/exercism/catalogs/alejandro.json
```

El catalogo versionado no debe guardar el enunciado completo del ejercicio:
solo metadata para pintar tarjetas y las URLs/Gist IDs publicos usados al
importar.

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

El proyecto no guarda ese token en Git ni en `usuario/`. El setup solo valida
si existe y muestra una instruccion si falta.

Para traducciones automaticas se lee primero la clave compartida del repo en
`_estudio/soporte/exercism/config.json`, luego `_estudio/soporte/exercism/config.local.json`,
despues `.estudio_exercism.local.json` y por ultimo `GEMINI_API_KEY` desde el
entorno. Si no existe ninguna, el import deja un README con traduccion pendiente.

### Extension Local

Fuente:

```text
_estudio/soporte/vscode/estudio-exercism/
```

Durante instalacion, `_estudio/installer/src/main/core/vscode.js` ejecuta:

1. `npm ci` dentro de la extension;
2. `npx vsce package --no-dependencies --allow-missing-repository --out ...`;
3. `code --install-extension _estudio/soporte/runtime/vscode/estudio-exercism.vsix --force`.

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
usuario/logs/<ejercicio>/bloqueN.log
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

`usuario/errores.md` empieza vacio en la version 1.0. La skill
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

- `_estudio/include/conio.h` expone las funciones usadas por ejercicios.
- `_estudio/soporte/consola/conio.c` implementa funciones sobre la consola de Windows.
- `_estudio/include/estudio_stdio_cp437.h` envuelve salida estandar para que codigos
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
_estudio/.agent/skills/revisar/SKILL.md
```

Debe reunir contexto por su cuenta y responder como tutor. Puede ser concreto,
pero no debe entregar el codigo final.

### `@ver`

Archivo:

```text
_estudio/.agent/skills/ver/SKILL.md
```

Hace prueba de escritorio RAM sobre una funcion, `main`, un ciclo o todo el
archivo segun la linea indicada.

### `@sintetizar`

Archivo:

```text
_estudio/.agent/skills/sintetizar/SKILL.md
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
2. verifica `npm run check` y `npm run installer:build`;
3. mergea o empuja a `main`;
4. crea y publica un tag semantico (`v2.0.0`, por ejemplo);
5. deja que GitHub Actions adjunte `Estudio-Socratico-Setup-v2.0.0-x64.exe`
   y su SHA256 al release;
6. actualiza `pair` y ramas personales que deban heredar el framework.

## Release 2.0

La release 2.0 agrega el instalador/configurador visual:

- fuente en `_estudio/installer`;
- build reproducible con .NET 10, WinUI 3, Windows App SDK self-contained y
  WiX Burn;
- artefactos generados en `_estudio/installer/artifacts/`, ignorados por Git;
- modos instalar, reparar, reinstalar y desinstalar;
- integracion completa de GitHub, Exercism, VS Code, MSYS2/GCC/Make y runtime
  local.

## Release 1.2

La release 1.2 deja el proyecto en el modelo actual:

- fuentes vigentes del panel: Exercism C y Alejandro;
- Alejandro conserva 132 ejercicios curados y descarga los enunciados desde
  Gists al importar;
- W3Schools / w3resource queda retirado hasta una reimplementacion futura;
- el VSIX no empaqueta ejercicios completos, catálogos generados ni
  `instructions.md` locales;
- el workspace usa `usuario/` como carpeta local singular.

## Release 1.0

La release 1.0 debe quedar limpia:

- un solo `.c` de ejemplo: `Ejercicios/Blackjack.c`;
- sin logs historicos versionados;
- `usuario/errores.md` vacios;
- runtime generado ignorado por Git;
- setup capaz de preparar una maquina nueva;
- README principal para estudiantes;
- esta guia para mantenedores.
