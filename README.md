# Estudio Socratico 1.0

Estudio Socratico es un entorno local para estudiar **Fundamentos de
Programacion en C** con ayuda de inteligencia artificial, sin convertir la IA en
un atajo para que haga los ejercicios por ti.

La idea es simple: escribes C, compilas como en Code::Blocks, el sistema guarda
tu historial de intentos y la IA usa ese contexto para ayudarte a pensar mejor.

> [!NOTE]
> Este proyecto esta pensado para aprender. La IA puede explicar, guiar,
> revisar y ayudarte a visualizar tu codigo, pero el objetivo es que el
> razonamiento siga siendo tuyo.

## Que Problema Resuelve

Cuando estas aprendiendo C, muchas veces el problema no es solo "me dio error".
El problema real puede ser:

- no ver como cambian las variables en memoria;
- no entender por que un ciclo repite de mas o de menos;
- confundir una posicion de arreglo con el valor guardado ahi;
- no saber interpretar un error de `gcc`;
- perder el hilo de varios intentos durante una sesion larga;
- pedir ayuda a una IA y recibir una respuesta demasiado vaga o demasiado
  resuelta.

Estudio Socratico intenta ordenar todo eso en un flujo de estudio:

1. escribes tu ejercicio en `Ejercicios/`;
2. compilas con `F9`;
3. el programa se abre en una consola estilo Code::Blocks;
4. se guarda un log del intento;
5. puedes pedir `@revisar`, `@ver` o `@sintetizar` segun lo que necesites.

> [!TIP]
> Si vienes de Code::Blocks, la sensacion buscada es familiar: compilar,
> ejecutar en una ventana externa, ver `Process returned...` y cerrar con una
> tecla.

## Instalacion Rapida

### Caso 1: Primera vez en una computadora

1. Descarga o clona este repositorio.
2. Abre la carpeta del proyecto en VS Code o Antigravity.
3. Abre una terminal integrada dentro de esa carpeta.
4. Ejecuta:

```bat
setup\instalar.cmd
```

El setup intentara preparar lo necesario: Git, PowerShell, GCC/MSYS2,
herramientas locales del proyecto, VS Code y el atajo `F9`.

Durante el proceso te pedira tus datos de estudio:

- nombre corto para tu carpeta y rama, por ejemplo `axel` o `juan`;
- usuario de GitHub;
- nombre que aparecera en tus commits;
- correo para tus commits.

Con eso crea:

```text
.estudio_usuario
usuarios/<tu_usuario>/errores.md
usuarios/<tu_usuario>/logs/
```

Tambien deja preparada tu rama personal cuando Git lo permite.

### Caso 2: Ya tienes todo instalado

Puedes ejecutar el mismo comando:

```bat
setup\instalar.cmd
```

Si ya tienes las herramientas, el setup solo valida, actualiza lo necesario y
confirma que el proyecto este listo.

### Caso 3: Solo quieres comprobar sin cambiar nada

```bat
setup\instalar.cmd -SoloVerificar -SinWinget -SinExtensiones
```

Tambien funciona:

```bash
npm run check
```

### Caso 4: No quieres que instale cosas automaticamente

```bat
setup\instalar.cmd -SinWinget
```

Usa esta opcion si prefieres instalar Git, VS Code o MSYS2 por tu cuenta.

## Uso Diario

### 1. Abre un ejercicio

Los ejercicios viven en:

```text
Ejercicios/
```

En la version limpia del proyecto solo viene un ejemplo:

```text
Ejercicios/Blackjack.c
```

### 2. Compila con F9

Abre un archivo `.c` y presiona:

```text
F9
```

El sistema compila el archivo activo y, si todo sale bien, abre una consola
externa estilo Code::Blocks.

> [!IMPORTANT]
> Mientras esa consola este abierta, el proyecto considera que hay una ejecucion
> en curso. Cierra la ventana o presiona una tecla en ella antes de volver a
> compilar.

### 3. Revisa los resultados

Cada intento queda registrado por usuario:

```text
usuarios/<tu_usuario>/logs/<ejercicio>/bloqueN.log
```

El sistema tambien mantiene:

```text
usuarios/<tu_usuario>/errores.md
```

Ese archivo empieza vacio en la version 1.0. Con el tiempo, `@sintetizar` lo
puede convertir en una memoria de patrones: errores frecuentes, conceptos que se
repiten y pistas para estudiar mejor.

## Skills De IA

Las skills son protocolos para que la IA se comporte como tutor y use el
contexto correcto del proyecto.

### `@revisar`

Usala cuando estas atascado y quieres una pista.

```text
@revisar
```

La IA debe leer tu usuario, tu archivo `.c`, tu `errores.md` y el ultimo log si
hace falta. Luego responde con una pista socratica, pero no vaga: debe explicar
el concepto de C que esta detras del problema.

> [!WARNING]
> `@revisar` no deberia darte la solucion completa ni escribir el codigo final.
> Debe ayudarte a pensar el siguiente paso.

### `@ver`

Usala cuando no logras visualizar que esta haciendo tu codigo.

Ejemplos:

```text
@ver linea 1
@ver linea 87
@ver main
@ver la funcion donde esta el cursor
@ver el ciclo de esta linea
@ver la linea del puntero
```

`@ver` hace una prueba de escritorio RAM: explica que variables existen, que
valores cambian, que decisiones toma el programa y que se imprime.

### Como usar lineas con `@ver`

En VS Code puedes ver el numero de linea a la izquierda del editor. Tambien lo
ves abajo en la barra de estado, por ejemplo `Ln 87`.

Cuando pidas ayuda, escribe el numero:

```text
@ver linea 87
```

La IA usara esa linea como punto de entrada. Segun donde caiga, el alcance
cambia:

| Si la linea apunta a... | `@ver` debe mirar... |
|---|---|
| linea `1` | todo el archivo del ejercicio |
| `main` | todo el `main` |
| nombre o encabezado de una funcion | esa funcion completa |
| `for`, `while` o `do` | ese ciclo completo |
| una linea dentro de un ciclo | el ciclo mas interno |
| una declaracion, asignacion o llamada | esa instruccion y su unidad logica |
| un puntero, arreglo, `struct`, `malloc` o archivo | desde esa linea hasta que termine la accion que pretende |

> [!TIP]
> Una "unidad logica" no siempre es una sola linea. Si apuntas a un puntero, por
> ejemplo, la IA puede incluir la comprobacion inmediata, el uso cercano o la
> liberacion del recurso si eso es necesario para explicar que pasa en RAM.

Si apuntas a:

- linea `1`: mira el archivo completo del ejercicio;
- `main`: mira todo `main`;
- una funcion: mira esa funcion;
- un `for`, `while` o `do`: mira ese ciclo;
- una linea suelta: mira desde el primer texto real de esa linea hasta donde
  termina la logica que esa linea esta intentando realizar.

### `@sintetizar`

Usala al final de una sesion de estudio.

```text
@sintetizar
```

La IA revisa commits y logs de la sesion, detecta patrones y actualiza
`usuarios/<tu_usuario>/errores.md`.

## Como Funcionan Los Logs

Los logs no son castigo ni vigilancia. Son una libreta de laboratorio.

Cada vez que compilas, el sistema guarda:

- fecha y hora del intento;
- archivo compilado;
- copia del codigo en ese momento;
- salida de `gcc`;
- resultado de ejecucion;
- codigo de salida.

Los bloques se agrupan aproximadamente cada 45 minutos:

```text
bloque1.log
bloque2.log
bloque3.log
```

> [!NOTE]
> En GitHub, la version 1.0 sale limpia: sin logs historicos y con los
> `errores.md` vacios. Los logs nuevos se crean cuando cada estudiante empieza a
> trabajar.

## Ramas Recomendadas

El proyecto esta pensado para compartir el mismo framework sin mezclar los
intentos de cada persona.

| Rama | Uso |
|---|---|
| `main` | Version estable del framework |
| `axel`, `eric`, `juan`, etc. | Trabajo personal de cada estudiante |
| `pair` | Trabajo compartido o Live Share |

Cuando una persona empieza, el setup puede crear o activar su rama personal con
el mismo nombre que su slug.

## Compatibilidad Con `conio.h`

El repo incluye soporte local para usar:

```c
#include <conio.h>
```

La intencion es que ejercicios con `gotoxy`, `clrscr`, `getch`, colores y salida
de consola se sientan lo mas parecido posible a Code::Blocks en Windows.

No tienes que copiar una libreria externa para compilar desde este entorno. El
script ya conecta `include/` y el runtime local cuando presionas `F9`.

## Donde Esta Cada Cosa

| Carpeta o archivo | Para que sirve |
|---|---|
| `Ejercicios/` | Tus archivos `.c` |
| `usuarios/` | Logs y errores por estudiante |
| `.agent/skills/` | Protocolos de IA como `@revisar` y `@ver` |
| `soporte/scripts/` | Scripts de compilacion y commits automaticos |
| `soporte/consola/` | Runtime de consola estilo Code::Blocks |
| `setup/` | Instalador y configuracion inicial |
| `docs/guia-tecnica.md` | Documentacion tecnica detallada |

## Documentacion Tecnica

Este README evita entrar demasiado en detalles internos. Si quieres entender o
modificar el motor del proyecto, lee:

```text
docs/guia-tecnica.md
```

Ahi se explica el flujo de scripts, logs, ramas, `conio.h`, runtime, setup y
reglas para mantener el framework.

## Problemas Comunes

### F9 no compila

Ejecuta otra vez:

```bat
setup\instalar.cmd
```

Luego cierra y vuelve a abrir VS Code si el atajo no aparece de inmediato.

### Dice que ya hay una ejecucion abierta

Cierra la consola externa del programa o presiona una tecla dentro de ella. Si
cerraste la ventana y el mensaje sigue apareciendo, vuelve a intentar; el lock
viejo se limpia automaticamente cuando detecta que el proceso ya no existe.

### GitHub muestra commits con otro nombre

Ejecuta el setup y revisa los datos que escribiste. Para GitHub, el correo debe
ser uno verificado o tu correo `noreply` de GitHub.

### La IA responde demasiado general

Pidele que lea el protocolo:

```text
Lee AGENTS.md y usa @revisar.
```

Para visualizar ejecucion, no uses `@revisar`; usa:

```text
@ver linea <numero>
```
