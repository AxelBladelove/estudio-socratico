<div align="center">

# 🏛️ Estudio Socrático

**Un entorno de estudio para aprender Fundamentos de Programación en C,<br>con una IA que te guía sin resolver los ejercicios por ti.**

![Versión](https://img.shields.io/badge/versión-2.0.0-blue?style=flat-square)
![Plataforma](https://img.shields.io/badge/plataforma-Windows-0078D6?style=flat-square&logo=windows)
![Lenguaje](https://img.shields.io/badge/lenguaje-C-A8B9CC?style=flat-square&logo=c)
![Editor](https://img.shields.io/badge/editor-VS_Code-007ACC?style=flat-square&logo=visualstudiocode)

</div>

---

## ¿Qué es esto?

Estudio Socrático convierte **VS Code** en un entorno parecido a Code::Blocks,
pero con una diferencia importante: tiene una **IA integrada que actúa como
tutor**.

La IA **no te da las respuestas**. Te hace preguntas, te explica conceptos y te
ayuda a descubrir qué está mal en tu código. El razonamiento siempre es tuyo.

> [!NOTE]
> Está pensado para el curso de **Fundamentos de Programación** de la PUCMM con
> el profesor Alejandro Liz, pero funciona para cualquier persona que esté
> aprendiendo C.

---

## Versión 2.0.0

La version 2.0 deja el proyecto listo como base limpia para empezar desde cero:

- **Instalador visual Textual** como entrada principal, con progreso, log,
  botones, flechas, reintentos y cambio de cuenta GitHub.
- **Backend self-contained en C#** para instalar, actualizar, reinstalar,
  desinstalar, verificar y empaquetar sin depender de Python en la PC del
  estudiante.
- **Release limpio**: el framework vive en `main`; los logs, alias y memoria del
  estudiante se generan localmente al usar el proyecto.
- **Alejandro** sigue como catalogo curado desde Gists y **Exercism C** conserva
  sus tests oficiales.

---

## ¿Qué problema resuelve?

Cuando aprendes C, los errores no siempre son fáciles de entender:

| El problema... | Lo que hace Estudio Socrático |
|---|---|
| `gcc` muestra un error y no sabes qué significa | La IA te explica el concepto detrás del error |
| Tu programa compila pero da resultados raros | Puedes pedir una "prueba de escritorio" paso a paso |
| No entiendes por qué un ciclo repite de más | La IA te muestra cómo cambian las variables |
| Pierdes el hilo de tus intentos | Cada compilación queda guardada automáticamente |
| Le preguntas a una IA y te da la solución directa | Aquí la IA te guía, no te resuelve |

---

## Lo que necesitas

- 💻 Una computadora con **Windows**
- 🐙 Una cuenta en **[GitHub](https://github.com/signup)** (es gratis)
- 🌐 Conexión a internet (solo durante la instalación)

> [!TIP]
> No necesitas tener nada más instalado. El instalador se encarga de todo:
> Git, GCC, VS Code y las herramientas del proyecto.

---

## Instalación

### Primera vez

```
1. Descarga o clona este repositorio
2. Abre la carpeta del proyecto
3. Haz doble clic en:
```

```bat
Instalar Estudio Socratico.cmd
```

Se abre el instalador visual de Estudio Socratico. Si usas el ZIP empaquetado
del setup, el archivo equivalente es:

```bat
Estudio.Setup.cmd
```

El instalador te pide solo dos cosas:

- **Tu nombre corto** — un alias como `axel` o `juan`, que identifica tus
  archivos dentro del proyecto.
- **Iniciar sesión en GitHub** — para vincular tus commits.

Al terminar quedan listos:
- ✅ El atajo <kbd>F9</kbd> para compilar
- ✅ Tu carpeta personal (`usuario/`)
- ✅ El historial de intentos vacío y listo para empezar
- ✅ El panel de ejercicios dentro de VS Code

<details>
<summary><strong>🔄 Ya lo tengo instalado, solo quiero actualizar</strong></summary>

Haz doble clic en:

```bat
Actualizar Estudio Socratico.cmd
```

O desde la terminal:

```bash
npm run setup:update
```

Esto valida tu instalación, actualiza lo que haga falta y no cambia tus archivos
personales.

</details>

<details>
<summary><strong>♻️ Quiero reinstalar integraciones locales</strong></summary>

Haz doble clic en:

```bat
Reinstalar Estudio Socratico.cmd
```

O desde la terminal:

```bash
npm run setup:reinstall
```

Esto reaplica configuracion local, extension, alias y componentes reparables sin
borrar tus ejercicios.

</details>

<details>
<summary><strong>🧹 Quiero desinstalar integraciones locales</strong></summary>

Haz doble clic en:

```bat
Desinstalar Estudio Socratico.cmd
```

O desde la terminal:

```bash
npm run setup:uninstall
```

Esto quita la extension/configuracion local de Estudio, `.estudio_usuario`,
config local Gemini y entradas PATH agregadas por el setup. No desinstala Git,
VS Code, Node ni herramientas globales del sistema.

</details>

<details>
<summary><strong>🔍 Solo quiero verificar sin cambiar nada</strong></summary>

```bash
npm run check
```

Esto revisa que todo esté en orden sin instalar ni modificar nada.

</details>

<details>
<summary><strong>🛠️ Prefiero instalar Git/GCC/VS Code por mi cuenta</strong></summary>

```bat
_estudio\setup\instalar.cmd -SinWinget
```

Esto omite las instalaciones automáticas y solo configura el proyecto.

</details>

---

## Uso diario

### 1️⃣ Abre tu archivo `.c`

Todos tus ejercicios van en la carpeta `Ejercicios/`. El proyecto trae un
directorio limpio para que empieces desde cero. Puedes crear archivos como:

```
Ejercicios/Blackjack.c
```

Puedes crear más archivos ahí cuando quieras.

### 2️⃣ Presiona <kbd>F9</kbd> para compilar y ejecutar

<kbd>F9</kbd> hace lo mismo que el botón de ejecutar en Code::Blocks:

1. Compila tu código con GCC.
2. Si hay errores, los muestra en la terminal.
3. Si compila bien, abre una **ventana de consola** con tu programa corriendo.
4. Al terminar, la consola muestra `Process returned...` como en Code::Blocks.

> [!IMPORTANT]
> Mientras la consola de tu programa esté abierta, no puedes compilar de nuevo.
> Cierra esa ventana (o presiona cualquier tecla en ella) antes de volver a
> presionar <kbd>F9</kbd>.

### 3️⃣ Cada intento queda guardado

No tienes que hacer nada. Cada vez que compilas, el sistema guarda
automáticamente:

- 🕐 La hora del intento
- 📄 El código que tenías en ese momento
- ❌ Los errores que mostró `gcc` (si hubo)
- ✅ El resultado de la ejecución
- 🔢 El código de salida del programa

Los registros se agrupan en bloques de ~45 minutos:

```
usuario/logs/nombre_del_ejercicio/
├── bloque1.log
├── bloque2.log
└── bloque3.log
```

> [!NOTE]
> Además, cada compilación crea un **commit automático** en Git con tu nombre.
> No tienes que hacer nada — esto pasa en segundo plano.

---

## Pide ayuda a la IA

La IA está integrada en el chat del editor. Puedes escribirle en español.
Hay cuatro comandos:

### `@revisar` — *Estoy atascado, dame una pista*

```
@revisar
```

La IA lee tu código, tus errores anteriores y el último intento. Te explica el
concepto de C que está detrás del problema y te hace una pregunta para que lo
pienses tú.

> [!WARNING]
> `@revisar` **no te da la solución**. Te ayuda a pensar el siguiente paso.
> Si quieres salir del modo tutor, díselo explícitamente.

---

### `@ver` — *No entiendo qué hace este código*

Úsalo cuando tu programa compila pero no entiendes qué pasa dentro.

```
@ver linea 1
@ver main
@ver linea 45
@ver el ciclo de esta linea
@ver la funcion donde esta el cursor
```

La IA hace una **prueba de escritorio**: te muestra paso a paso cómo cambian
las variables, qué decisiones toma el programa y qué se imprime en pantalla.

<details>
<summary><strong>¿Cómo sé el número de línea?</strong></summary>

Mira la barra de estado de VS Code, abajo a la izquierda. Dice algo como
`Ln 45`. También puedes ver los números al lado izquierdo del código.

</details>

| Si escribes... | La IA analiza... |
|---|---|
| `@ver linea 1` | Todo el archivo completo |
| `@ver main` | Todo el bloque `main` |
| `@ver linea 45` | La función o ciclo donde está esa línea |
| `@ver el for de esta linea` | Ese ciclo completo |
| `@ver la linea del puntero` | Desde esa línea hasta donde termina la acción |

> [!TIP]
> Si apuntas a un puntero, arreglo, `struct` o `malloc`, la IA puede incluir
> líneas cercanas (como la comprobación o el `free`) para explicar qué pasa en
> la memoria.

---

### `@sintetizar` — *Terminé la sesión de hoy*

```
@sintetizar
```

Úsalo **al final de cada sesión de estudio**. La IA revisa todos los commits y
logs de la sesión, detecta patrones de error y actualiza tu archivo personal:

```
usuario/errores.md
```

Ese archivo se convierte en tu memoria de estudio: errores frecuentes,
conceptos que se repiten y pistas para estudiar antes de un examen.

> [!NOTE]
> `errores.md` nunca se borra automáticamente. Cada sesión agrega información
> nueva o incrementa la frecuencia de patrones que ya existían.

---

### `@test` / `@validar` — *Quiero comprobar mi solución*

```
@test
@validar
```

Úsalo en ejercicios del **PDF del profesor** cuando quieras
saber si tu respuesta es correcta. La IA:

1. Lee el enunciado y tu archivo `.c`
2. Crea pruebas automáticas en `.estudio-tests/`
3. Las ejecuta sin tocar tu código
4. Si todo pasa, el ejercicio queda marcado como completado

> [!IMPORTANT]
> Para ejercicios de **Exercism**, no necesitas `@test`. Exercism ya trae sus
> propios tests oficiales y se ejecutan con <kbd>F9</kbd>.

---

## Panel de ejercicios

El proyecto incluye un **panel dentro de VS Code** para explorar e importar
ejercicios de dos fuentes:

| Fuente | Descripción |
|---|---|
| **Exercism C** | Plataforma gratuita con ejercicios de programación en C |
| **PDF Alejandro Liz** | Ejercicios del profesor del curso |

W3Schools / w3resource fue retirado en la version 1.2. Se planea
reintroducirlo desde cero en una version futura con curaduria semantica.

### Abrir el panel

```
Ctrl + Shift + P → "Estudio Socratico: Abrir Panel de Ejercicios"
```

También puedes abrirlo desde el botón del editor cuando tienes un archivo `.c`
abierto.

### Importar un ejercicio

1. Filtra por la fuente que prefieras (ej. `Exercism C`)
2. Elige un ejercicio, por ejemplo `Grains`
3. Haz clic en la tarjeta

El ejercicio se descarga y las instrucciones se traducen al español
automáticamente:

```
Ejercicios/Grains/
├── grains.c          ← Tu solución (aquí escribes)
├── grains.h          ← Cabecera del ejercicio
└── .estudio-exercism/ ← Tests y metadata (oculto)
```

> [!NOTE]
> Los archivos técnicos (tests, makefiles, metadata) quedan ocultos en carpetas
> que empiezan con `.estudio-`. Solo ves los archivos donde escribes tu
> solución.

### <kbd>F9</kbd> cambia de modo automáticamente

| Archivo activo | Qué hace <kbd>F9</kbd> |
|---|---|
| `Ejercicios/Blackjack.c` u otro `.c` normal | Compila y ejecuta en consola |
| `Ejercicios/Grains/grains.c` (Exercism) | Corre los tests oficiales |

Desde el panel también puedes:

- **Probar** — ejecuta los tests locales
- **Enviar** — envía tu solución a Exercism (`exercism submit`)
- **Actualizar** — refresca el catálogo y los estados

<details>
<summary><strong>⚙️ Configurar Exercism (solo una vez)</strong></summary>

Exercism usa un token personal que se configura una sola vez en tu computadora:

```bat
exercism configure --token TU_TOKEN_AQUI
```

El instalador te avisa si falta. El token **no se guarda** en el repositorio.

> [!TIP]
> Puedes encontrar tu token en https://exercism.org/settings/api_cli

</details>

---

## Tu carpeta personal

Cuando te instalas por primera vez, el proyecto crea una carpeta solo para ti:

```
usuario/
├── logs/              ← Historial de todos tus intentos
│   ├── Blackjack/
│   │   ├── bloque1.log
│   │   └── bloque2.log
│   └── Grains/
│       └── bloque1.log
└── errores.md         ← Tu memoria de estudio (la llena @sintetizar)
```

Nadie más ve tu `errores.md`. Es tu resumen personal de patrones de error.

---

## Compatibilidad con `conio.h`

Si el ejercicio usa funciones como `gotoxy`, `clrscr`, `getch` o colores de
consola, **no necesitas instalar nada extra**. El proyecto incluye soporte
local para:

```c
#include <conio.h>
```

Funciones disponibles:

| Función | Para qué sirve |
|---|---|
| `gotoxy(x, y)` | Mover el cursor a una posición |
| `clrscr()` | Limpiar la pantalla |
| `getch()` | Leer una tecla sin mostrarla |
| `getche()` | Leer una tecla mostrándola |
| `kbhit()` | Saber si se presionó una tecla |
| `textcolor(color)` | Cambiar el color del texto |
| `textbackground(color)` | Cambiar el color de fondo |
| `wherex()` / `wherey()` | Saber la posición del cursor |

> [!TIP]
> Los caracteres especiales de la tabla CP437 (como ♥, ♦, ♣, ♠ y bordes de
> recuadro) también se muestran correctamente sin configuración extra.

---

## ¿Dónde está cada cosa?

```
estudio-socratico/
├── Ejercicios/                ← Tus archivos .c van aquí
├── usuario/                   ← Logs, progreso y errores del estudiante
│   ├── logs/
│   └── errores.md
├── _estudio/                  ← Motor interno, setup, docs, extensión y tooling
├── Instalar Estudio Socratico.cmd
├── Actualizar Estudio Socratico.cmd
├── Reinstalar Estudio Socratico.cmd
└── Desinstalar Estudio Socratico.cmd
```

---

## Git

> No necesitas saber nada de Git para usar esto. El instalador configura todo y
> los commits se hacen solos cada vez que compilas.

La version base del framework vive en `main`. Tu identidad local vive en
`.estudio_usuario`, tus intentos en `usuario/logs/` y tu memoria de estudio en
`usuario/errores.md`. Esos datos son locales del clon del estudiante.

---

## Algo no funciona

<details>
<summary><strong>❌ <kbd>F9</kbd> no hace nada</strong></summary>

Ejecuta el instalador de nuevo:

```bat
Instalar Estudio Socratico.cmd
```

Luego cierra VS Code completamente y vuelve a abrirlo.

</details>

<details>
<summary><strong>⚠️ Dice "ya hay una ejecución abierta"</strong></summary>

Tienes una ventana de consola abierta de una ejecución anterior. Ciérrala o
presiona cualquier tecla dentro de ella.

Si ya la cerraste y el error persiste, vuelve a intentar compilar — el sistema
detecta automáticamente que el proceso anterior ya no existe.

</details>

<details>
<summary><strong>🔀 GitHub muestra commits con otro nombre</strong></summary>

Ejecuta el instalador y revisa los datos de tu cuenta. Para que GitHub asocie
correctamente tus commits, el correo debe ser uno verificado en tu cuenta o tu
correo `noreply` de GitHub.

</details>

<details>
<summary><strong>🤖 La IA responde de forma muy general</strong></summary>

Escríbele esto en el chat:

```
Lee AGENTS.md y usa @revisar.
```

Para visualizar ejecución, no uses `@revisar`. Usa:

```
@ver linea <numero>
```

</details>

---

## Preguntas frecuentes

<details>
<summary><strong>¿Tengo que saber usar Git?</strong></summary>

No. Git corre en segundo plano. Tú solo presionas <kbd>F9</kbd>. Los commits se
hacen automáticamente con tu nombre.

</details>

<details>
<summary><strong>¿Puedo usar <code>conio.h</code> y <code>gotoxy</code>?</strong></summary>

Sí. El proyecto incluye soporte completo para `conio.h` en Windows. Funciones
como `getch`, `clrscr`, `gotoxy` y colores compilan con <kbd>F9</kbd> sin
configuración extra.

</details>

<details>
<summary><strong>¿Funciona en Mac o Linux?</strong></summary>

Por ahora está pensado solo para **Windows**. Los scripts de compilación, la
consola estilo Code::Blocks y el soporte de `conio.h` son específicos de
Windows.

</details>

<details>
<summary><strong>¿Funciona con Live Share?</strong></summary>

Sí. El proyecto incluye una tarea alternativa para sesiones de Live Share que
ejecuta el programa en la terminal compartida en lugar de abrir una ventana
externa.

</details>

<details>
<summary><strong>¿Dónde está la documentación técnica?</strong></summary>

En [`_estudio/docs/guia-tecnica.md`](_estudio/docs/guia-tecnica.md). Ahí se explica la
arquitectura interna: scripts, logs, ramas, runtime, setup y cómo mantener el
framework.

</details>

---

<div align="center">

**Estudio Socrático** · v2.0.0 · Mayo 2026

*Hecho para aprender, no para copiar.*

</div>
