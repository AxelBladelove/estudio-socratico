# Guia Tecnica Del Framework

Esta guia es para quien quiera mantener, extender o depurar Estudio Socratico.
El README principal esta escrito para estudiantes; este archivo explica el
motor.

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
- configura Git local;
- escribe `.estudio_usuario`;
- crea `usuarios/<slug>/errores.md` vacio si falta;
- crea o cambia a la rama personal cuando es posible;
- valida GCC/MSYS2;
- compila `soporte/runtime/_output.exe`;
- compila `soporte/runtime/conio_support.o`;
- configura `F9` en VS Code.

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
- abre el ejecutable en una ventana externa;
- bloquea ejecuciones duplicadas mediante `soporte/runtime/run.lock`;
- difiere el commit automatico hasta que el estudiante cierre la ventana;
- guarda salida del programa en el log cuando el launcher recibe `--log`.

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
