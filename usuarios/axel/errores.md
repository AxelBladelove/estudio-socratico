## Aritmética — Operaciones en orden incorrecto al descomponer unidades jerárquicas
**Frecuencia:** 1
**Modelo mental roto:** Cuando un valor debe descomponerse en unidades de diferente tamaño (mayor → menor), aplicar el módulo antes de la división entera destruye el valor original antes de que puedas extraer la parte mayor. El orden correcto siempre es: divide para saber "cuántos de la unidad grande caben", luego el módulo para saber "qué sobra".
**Cómo apareció hoy:** Intentó extraer la unidad menor (pies) antes que la mayor (yardas), y previamente aplicó módulo antes de la división, eliminando el valor original prematuramente.
**Pista para cuando vuelva a aparecer:** Si tienes una montaña de monedas y quieres agruparlas en billetes de 100 y con lo que sobre en billetes de 10, ¿cuál agrupas primero para no quedarte sin monedas para la siguiente?

---

<!--
INSTRUCCIONES PARA IA — NO BORRAR

CONTEXTO DEL ESTUDIANTE:
Este archivo documenta los patrones de error de un estudiante de primer semestre de
Ingeniería en Computación y Telemática (PUCMM), preparándose para el 2do Parcial del
Prof. Alejandro Liz (structs con memoria dinámica, recursión, archivos binarios).

PROPÓSITO DE ESTE ARCHIVO:
No es un registro de errores de una sesión. Es una base de conocimiento acumulativa
de MODELOS MENTALES ROTOS — creencias incorrectas que producen errores una y otra
vez en ejercicios diferentes. El estudiante lo lee antes del examen para reconocer
estos patrones antes de que aparezcan.

REGLAS:
- NUNCA borrar ni modificar secciones existentes, salvo para incrementar Frecuencia.
- Si el mismo modelo mental roto apareció hoy: encuentra la sección por el título
  o por "Cómo apareció hoy" e incrementa Frecuencia en 1. No toques el resto.
- Si es un patrón nuevo: agrega una nueva sección al final, antes de este comentario.

SOBRE EL MODELO MENTAL ROTO:
No expliques cómo resolver el error. Expón la CREENCIA FALSA subyacente.
Debe ser reconocible en ejercicios completamente diferentes al de hoy.
❌ MAL: "Para descomponer unidades, divide primero y luego aplica módulo."
✅ BIEN: "Aplicar el módulo antes de la división destruye el valor original antes
de poder extraer la unidad mayor."

SOBRE LA PISTA:
Una sola pregunta. Activa el modelo correcto sin revelar la respuesta.
Funciona sin que el estudiante recuerde el ejercicio donde cometió el error.
❌ MAL: "¿Estás dividiendo antes de aplicar el módulo?" (señala directamente)
✅ BIEN: analogía física que cambia el ángulo de ataque.

CATEGORÍAS DISPONIBLES:
Punteros · Memoria Dinámica · Aritmética · Strings · Estructuras · I/O ·
Lógica de Control · Recursión · Otro
-->
