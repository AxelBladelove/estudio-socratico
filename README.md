# Sistema de Estudio Socratico

**Arquitectura Hibrida v4.1** - VS Code listo. Multi-IA. Friccion baja.
Documentacion automatica.

## Que hace este sistema

Convierte tu editor en una caja negra de estudio: cada vez que compilas, el
sistema registra silenciosamente tu codigo y el resultado. Al final del bloque,
una IA analiza la sesion completa y actualiza tu base de conocimiento de errores.

Funciona con VS Code, Antigravity y cualquier asistente que pueda leer archivos
del workspace: Codex en VS Code, OpenCode, Roo Code, Gemini, Claude u otro
cliente con acceso a esta carpeta.

## Instalacion Rapida

La ruta oficial del proyecto es usar un gestor JS. Si tienes Node.js, ya tienes
`npm` y normalmente tambien `npx`.

```bash
npm run setup
```

Tambien estan soportados:

```bash
pnpm run setup
```

```bash
bun run setup
```

```bash
npx --yes npm@latest run setup
```

El instalador asume que VS Code o Antigravity ya estan instalados. Verifica o
instala Git y MSYS2/GCC, prepara las carpetas del proyecto, configura Git
localmente, instala las extensiones recomendadas de VS Code y valida la
configuracion del workspace. Se puede ejecutar varias veces sin romper la
configuracion.

Para ver lo que haria sin instalar nada:

```bash
npm run setup:dry
```

## Setup Manual

### 1. Requisitos

- `gcc` instalado y en el `PATH` (MinGW/MSYS2 en Windows).
- `git` instalado.
- La carpeta `estudio-socratico/` abierta como workspace en VS Code,
  Antigravity o un editor compatible con tareas de VS Code.

### 2. Inicializar Git

Abre la terminal integrada (`Ctrl+``) y ejecuta:

```bash
git init
git config user.email "estudiante@estudio.local"
git config user.name "Estudiante"
git add .
git commit -m "setup_inicial"
```

### 3. Compilar con Ctrl+Shift+B

El archivo `.vscode/tasks.json` ya esta configurado. El atajo predeterminado
para "Run Build Task" en VS Code/Antigravity es:

- Windows: `Ctrl+Shift+B`

Para asignarlo a `F5` o cualquier otra tecla en VS Code:
`Archivo > Preferencias > Atajos de Teclado` -> busca "Run Build Task" ->
cambia la tecla.

### 4. Configurar tu IA preferida

El repositorio trae instrucciones portables:

| Herramienta | Archivo que debe leer |
|---|---|
| Codex en VS Code | `AGENTS.md` y `.vscode/codex-instructions.md` |
| OpenCode / agentes compatibles | `AGENTS.md` |
| Antigravity u otros clientes con skills | `.agent/skills/revisar/SKILL.md` y `.agent/skills/sintetizar/SKILL.md` |

Si tu cliente no reconoce `@revisar` o `@sintetizar`, escribe:

```text
Lee AGENTS.md y ejecuta el protocolo revisar sobre el archivo C activo.
```

o:

```text
Lee AGENTS.md y ejecuta el protocolo sintetizar para la sesion de hoy.
```

## Flujo de Trabajo Diario

### Al comenzar un ejercicio nuevo

Crea un archivo `.c`, por ejemplo `Ejercicios/ejercicio_01.c`. La linea 1 es
importante: el Contrato Logico debe ser lo primero que escribes.

```c
/* Ejercicio: Leer N numeros enteros usando memoria dinamica,
   e imprimir la suma de los que sean primos. */

#include <stdio.h>
#include <stdlib.h>
```

### Durante la codificacion

1. Programa normalmente.
2. Cuando quieras compilar y ejecutar, presiona `Ctrl+Shift+B`.
3. El script compila con `gcc`, ejecuta el programa si compilo bien, registra
   todo en `logs/<nombre_del_ejercicio>.log` y hace un commit automatico.
4. Tu solo ves el resultado en la terminal. Sin latencia. Sin internet.

Repite este ciclo las veces que necesites. Cada intento queda grabado.

### Si te quedas atascado

Abre el chat de tu IA. Si soporta skills, escribe:

```text
@revisar
```

La IA leera tu codigo completo y te dara una pista socratica. Puede agregar una
micro-explicacion tecnica si detecta que falta un concepto base, por ejemplo
stack, heap, punteros, acumuladores o alcance. No te dara codigo ni te dira la
linea exacta.

### Al final del bloque de estudio

Abre el chat de tu IA. Si soporta skills, escribe:

```text
@sintetizar
```

La IA analizara los commits y logs de la sesion, y actualizara `errores.md`
con patrones de error reutilizables para estudiar antes del examen.

## Estructura del Proyecto

```text
estudio-socratico/
|-- AGENTS.md                         Instrucciones portables para agentes IA
|-- .vscode/codex-instructions.md     Instrucciones para Codex en VS Code
|-- .vscode/tasks.json                Build con Ctrl+Shift+B
|-- .agent/skills/
|   |-- revisar/SKILL.md              Protocolo de pista socratica
|   `-- sintetizar/SKILL.md           Protocolo de cierre de sesion
|-- setup_laptop.ps1                  Instalador/verificador principal
|-- package.json                      Atajos npm/pnpm/bun/npx: setup, setup:dry, check
|-- compilar_y_grabar.bat             Script local de compilacion y telemetria
|-- errores.md                        Base de conocimiento acumulativa
|-- logs/*.log                        Historial por ejercicio
|-- .gitignore                        Excluye ejecutables y temporales
`-- Ejercicios/*.c                    Tus ejercicios
```

## Reglas del Sistema

| Regla | Descripcion |
|---|---|
| Contrato Logico | La linea 1 de todo `.c` debe ser el enunciado del ejercicio en comentario multilínea |
| Ctrl+Shift+B | Atajo principal para compilar; evita usar `gcc` directo durante la sesion si quieres telemetria completa |
| @revisar | Solo cuando estas atascado; no usarlo de forma rutinaria |
| @sintetizar | Solo una vez al terminar el bloque; nunca a mitad de sesion |
| errores.md | No editar manualmente durante la sesion; la IA lo modifica al sintetizar |

## Troubleshooting

**El script no se ejecuta al presionar Ctrl+Shift+B:**
verifica que el workspace abierto sea la carpeta `estudio-socratico/`.

**`gcc` no se encuentra:**
instala MSYS2/MinGW y agrega `C:\msys64\mingw64\bin` o tu ruta de MinGW al
`PATH`.

**El git commit falla silenciosamente:**
ejecuta `git init` y configura `user.email` / `user.name`.

**La IA no responde a `@sintetizar` o `@revisar`:**
usa la frase "lee AGENTS.md y ejecuta el protocolo revisar/sintetizar".
