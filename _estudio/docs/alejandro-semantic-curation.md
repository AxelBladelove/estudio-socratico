# Curaduría Semántica de Alejandro

Esta fase del pipeline realiza una segunda pasada inteligente sobre los ejercicios previamente parseados. Dado que el parseo original (fase 1) dependía de la estructura visual del PDF y asignaba filtros genéricos como "Sección 1" o "Página X", la curaduría semántica soluciona esto analizando el *contenido* de la instrucción del ejercicio.

## 1. ¿Por qué esto no es parsing?

El proceso de parsing extrajo el texto del PDF de forma mecánica. La curaduría semántica no usa el PDF, usa la instrucción ya extraída en `instructions.md`. Su objetivo es **clasificar**, no extraer. Es un proceso de enriquecimiento de metadata (título corto, descripción, filtros temáticos precisos, dificultad).

## 2. ¿Qué hace la IA?

La IA lee la instrucción y genera un JSON con:
- Un título representativo (ej: "Suma hasta N" en vez de "Problema 1").
- Una descripción de 1 línea útil para la UI de VS Code.
- Filtros técnicos precisos.
- Conceptos de búsqueda para Iconify.

## 3. ¿Qué NO puede inventar la IA?

Para mantener la integridad técnica, la IA está limitada por:
- Temperatura baja (0.1).
- Una **taxonomía estricta** (`_estudio/soporte/alejandro/semantic-taxonomy.json`). No puede usar filtros que no existan ahí.
- No puede inventar requisitos (ej: si el ejercicio no pide explícitamente "arreglos" o "punteros", la IA no debe añadir esos filtros).

## 4. Control de Filtros

La taxonomía está definida en `_estudio/soporte/alejandro/semantic-taxonomy.json`. El pipeline verifica estrictamente que los filtros devueltos por la IA pertenezcan a esa lista. Si no lo son, se rechaza y se marca como error.

## 5. Elección de Iconos

No le pedimos a la IA que invente código SVG (lo cual suele fallar o generar gráficos inconsistentes). La IA solo genera el `iconConcept` (ej: "calculator"). Luego, un script dedicado (`_estudio/soporte/alejandro/iconify-icons.mjs`) consulta la API pública de **Iconify** y descarga un icono oficial de colecciones limpias como Lucide o Tabler, garantizando un aspecto moderno y profesional.

## 6. Overrides Manuales

A veces la IA puede equivocarse de dificultad o elegir un filtro ligeramente incorrecto. Para ello existe el archivo:
`.generated/alejandro-semantic/manual-overrides.json`

Cualquier cambio hecho en este JSON sobreescribirá silenciosamente el resultado de la IA cuando se ejecute el paso de aplicar resultados.

## 7. Actualización de Gists

Después de aplicar los resultados semánticos a los ejercicios locales (con `npm run alejandro:semantic:apply`), los archivos `metadata.json` y `icon.svg` locales cambiarán.
Debes re-publicar los ejercicios a GitHub ejecutando:

\`\`\`bash
npm run alejandro:gists:publish
npm run alejandro:gists:manifest
\`\`\`

## 8. Casos de Baja Confianza

Los casos donde la IA falló o tuvo muy baja confianza se guardan en `.generated/alejandro-semantic/low-confidence.json`. Revisa este archivo para añadir `manual-overrides` si es necesario.

## Archivos Generados

- `.generated/alejandro-semantic/semantic-results.json` -> Resultados crudos de la IA. NO commitear (se puede regenerar).
- `.generated/alejandro-semantic/low-confidence.json` -> Errores o baja confianza.
- `.generated/alejandro-semantic/filter-distribution.md` -> Reporte markdown de métricas.
- `.generated/alejandro-semantic/manual-overrides.json` -> Archivo del usuario. SÍ commitear.
