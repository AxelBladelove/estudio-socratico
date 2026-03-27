# 🏛️ Sistema de Estudio Socrático

**Arquitectura Híbrida Definitiva v4.0** — Fricción cero. Tokens mínimos. Documentación automática.

---

## ¿Qué hace este sistema?

Convierte Antigravity en una "caja negra de avión": cada vez que compilas, el sistema registra
silenciosamente tu código y el resultado. Al final del bloque de estudio, la IA analiza toda
la sesión de una sola vez y actualiza tu base de conocimiento de errores.

---

## Setup Inicial (Solo una vez)

### 1. Requisitos
- [gcc](https://www.mingw-w64.org/) instalado y en el PATH
- [git](https://git-scm.com/download/win) instalado
- La carpeta `estudio-socratico/` abierta como workspace en Antigravity

### 2. Inicializar Git en el proyecto

Abre la terminal en Antigravity (`Ctrl+\``) y ejecuta:

```bash
git init
git config user.email "estudiante@estudio.local"
git config user.name "Estudiante"
git add .
git commit -m "setup_inicial"
```

### 3. Asignar F5 o Ctrl+Shift+B como atajo de compilación

El archivo `.vscode/tasks.json` ya está configurado. El atajo predeterminado para
"Run Build Task" en Antigravity/VS Code es:

- **Windows**: `Ctrl+Shift+B`

Para asignarlo a `F5` o cualquier otra tecla, ve a:
`Archivo > Preferencias > Atajos de Teclado` → busca "Run Build Task" → cambia la tecla.

---

## Flujo de Trabajo Diario

### 📄 Al comenzar un ejercicio nuevo

Crea un archivo `.c` (ej. `ejercicio_01.c`). La **línea 1 es sagrada**:
el Contrato Lógico debe ser lo PRIMERO que escribes:

```c
/* Ejercicio: Leer N números enteros usando memoria dinámica,
   e imprimir la suma de los que sean primos. */

#include <stdio.h>
#include <stdlib.h>
// ... tu código aquí
```

### ⚡ Durante la codificación (Capa sin tokens)

1. Programa normalmente.
2. Cuando quieras compilar y ejecutar: presiona **`Ctrl+Shift+B`**.
3. El script hace tres cosas en silencio:
   - Compila tu código con gcc
   - Ejecuta el programa (si compiló bien)
   - Registra todo en `compiler_log.txt` y hace un `git commit`
4. Tú solo ves el resultado en la terminal. Sin latencia. Sin internet.

> Repite este ciclo las veces que necesites. Cada intento queda grabado.

### 🤔 Si te quedas atascado (Mecanismo 1)

Abre el chat lateral de Antigravity. Escribe simplemente:

```
@revisar
```

La IA leerá tu código completo y te dará **una sola pista socrática abstracta**.
No te dará código. No te dirá la línea del error. Te hará pensar.

### 📊 Al final del bloque de 45 minutos (Mecanismo 2)

Abre el chat lateral de Antigravity. Escribe:

```
@sintetizar
```

La IA analizará **todos tus commits de la sesión** y actualizará `errores.md`
con los patrones de error encontrados. Un único consumo de tokens, una sola vez al día.

### 📋 Exportar a Notion

Abre `errores.md`. Copia la tabla completa. Pégala en Notion (detecta la tabla automáticamente).

---

## Estructura del Proyecto

```
estudio-socratico/
├── .vscode/tasks.json          ← NO tocar. Controla la compilación.
├── .agent/skills/
│   ├── sintetizar/SKILL.md     ← NO tocar. La IA lo lee al invocar @sintetizar.
│   └── revisar/SKILL.md        ← NO tocar. La IA lo lee al invocar @revisar.
├── compilar_y_grabar.bat       ← NO tocar. Es el espía local.
├── errores.md                  ← Tu base de conocimiento acumulativa.
├── compiler_log.txt            ← Auto-generado. La IA lo lee para sintetizar.
├── .gitignore                  ← Excluye .exe y archivos temporales.
└── ejercicio_01.c              ← TUS ARCHIVOS van aquí (en la raíz)
```

---

## Reglas del Sistema

| Regla | Descripción |
|---|---|
| **Contrato Lógico** | La línea 1 de todo `.c` DEBE ser el enunciado del ejercicio en comentario multilínea |
| **F9 / Ctrl+Shift+B** | Único atajo para compilar. Nunca usar la terminal directa para gcc durante la sesión |
| **@revisar** | Solo cuando estás atascado. No usarlo de forma rutinaria (gasta tokens) |
| **@sintetizar** | Solo una vez al terminar el bloque. Nunca a mitad de sesión |
| **errores.md** | No editar manualmente. Solo la IA lo modifica al sintetizar |

---

## Troubleshooting

**El script no se ejecuta al presionar Ctrl+Shift+B:**
→ Verifica que el workspace abierto sea la carpeta `estudio-socratico/`

**"gcc: command not found":**
→ Instala MinGW y agrega `C:\mingw64\bin` a tu variable de entorno PATH

**El git commit falla silenciosamente:**
→ Ejecuta `git init` en la carpeta si aún no lo hiciste (ver Setup Inicial)

**La IA no responde a @sintetizar o @revisar:**
→ Los archivos SKILL.md deben estar en `.agent/skills/[nombre]/SKILL.md`
→ Verifica que la carpeta `.agent` esté en la raíz del workspace
