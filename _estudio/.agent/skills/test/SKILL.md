---
name: test
description: >
  Genera o actualiza tests locales para ejercicios no-Exercism de Estudio
  Socratico cuando el usuario escriba @test, @validar, "crea tests",
  "valida mi solucion" o pida comprobar un ejercicio PDF Alejandro sin tests
  oficiales. Usa el modelo IA activo del usuario, no la API key compartida de
  Gemini, y crea un harness oculto en .estudio-tests.
---

# Skill: Test / Validar

Actua como tutor tecnico de C. Tu objetivo es crear validacion, no resolver el
ejercicio. No reescribas la solucion del estudiante.

## Flujo

1. Lee `.estudio_usuario` y el archivo `.c` activo o indicado.
2. Sube hasta encontrar `.estudio-exercism.json`.
3. Si `provider` es `exercism`, no generes tests: indica que debe usarse
   `exercism test` o F9.
4. Lee el comentario inicial del `.c`, `README.md` si existe, o
   `.estudio-exercism/support/README.md` si el enunciado esta oculto.
5. Decide el modo de validacion:
   - `stdout`: el programa completo lee entrada e imprime salida.
   - `function`: el enunciado exige funciones concretas que pueden llamarse.
6. Crea o actualiza `.estudio-tests/manifest.json` y un runner.

## Layout obligatorio

Usa esta carpeta dentro del ejercicio:

```text
.estudio-tests/
  manifest.json
  validar.ps1
  README.md
```

`manifest.json` debe incluir:

```json
{
  "version": 1,
  "provider": "ai-generated",
  "mode": "stdout",
  "exerciseTitle": "Titulo",
  "solutionFiles": ["archivo.c"],
  "createdAt": "ISO-8601",
  "cases": []
}
```

`validar.ps1` debe aceptar estos parametros:

```powershell
param(
  [string]$RepoRoot,
  [string]$ExerciseRoot
)
```

Debe compilar con `C:\msys64\mingw64\bin\gcc.exe` si existe; si no, usar
`gcc` del PATH. Debe terminar con `exit 0` solo cuando todos los casos pasen.

## Reglas de calidad

- Cubre casos normales, bordes y errores de entrada cuando apliquen.
- No dependas de texto decorativo innecesario; valida valores y frases clave.
- No generes tests que obliguen una unica implementacion interna.
- Si el ejercicio no define formato de salida, valida el comportamiento
  minimo observable y documenta esa decision en `.estudio-tests/README.md`.
- Si no puedes crear tests confiables sin cambiar el contrato del ejercicio,
  explica la limitacion y pide al estudiante precisar entrada/salida esperada.

## Respuesta al estudiante

Despues de crear los tests, responde breve:

- que modo usaste (`stdout` o `function`);
- cuantos casos cubriste;
- como ejecutarlos: F9 si la extension lo detecta, o `@validar`;
- que supuesto importante hiciste si hubo alguno.
