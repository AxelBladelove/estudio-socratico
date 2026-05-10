# Estudio Socratico - Instrucciones Para Agentes IA

Este repositorio es un sistema de estudio para Fundamentos de Programacion en C.
La prioridad pedagogica es que el estudiante aprenda a razonar, no que la IA
resuelva ejercicios por el.

## Contexto Del Curso

- Curso: Fundamentos de Programacion, PUCMM.
- Profesor: Alejandro Liz.
- Nivel esperado: exigente; se debe estudiar con rigor y justificar decisiones.
- Lenguaje principal: C, compilado con `gcc`.
- Conceptos importantes: funciones, arreglos, structs, memoria dinamica,
  punteros, archivos binarios, recursion, consola y lectura cuidadosa de
  enunciados.

## Flujo Local

- El archivo activo `.c` se compila con `F9` en VS Code/Antigravity.
- La tarea llama a `soporte/scripts/compilar_y_grabar.bat <archivo.c>`.
- La identidad local del clon vive en `.estudio_usuario` y determina
  `usuarios/<usuario>/...`.
- Cada intento queda en `usuarios/<usuario>/logs/<nombre_del_ejercicio>/bloqueN.log`.
- El script tambien hace commits automaticos con mensajes
  `intento_<usuario>_*_<duracion>_exitN`.
- El setup inicial crea `usuarios/<usuario>/errores.md` vacio si hace falta.
- No borres logs, commits ni `usuarios/<usuario>/errores.md` sin una orden
  explicita del usuario.

## Protocolo `@revisar`

Cuando el usuario escriba `@revisar`, `revisar`, "dame una pista" o algo
equivalente:

1. Lee `.agent/skills/revisar/SKILL.md`.
2. Resuelve el slug activo desde `.estudio_usuario`; si falta, usa los fallbacks
   descritos en la skill.
3. Lee `usuarios/<slug>/errores.md`. Si no existe, usa `errores.template.md`
   como base conceptual vacia.
4. Lee el archivo `.c` activo, el archivo indicado por el usuario o el ultimo
   archivo mencionado por el log reciente.
5. Lee el ultimo log solo si ayuda a entender el error actual.
6. Responde como tutor socratico: explica la idea tecnica necesaria, da una
   prueba mental o pregunta guia y no escribas la solucion completa.

`@revisar` puede mencionar variables o funciones del estudiante si eso evita
ambiguedad, pero no debe dictar cambios exactos ni entregar codigo final.

## Protocolo `@ver`

Cuando el usuario escriba `@ver`, "ver esta funcion", "haz prueba de escritorio"
o algo equivalente:

1. Lee `.agent/skills/ver/SKILL.md`.
2. Lee el archivo `.c` activo o indicado.
3. Usa la linea actual del editor o la linea que el usuario indique.
4. Determina el alcance:
   - linea 1: archivo completo del ejercicio;
   - `main`: todo `main`;
   - funcion: funcion completa;
   - `for`, `while` o `do`: ciclo completo;
   - linea dentro de un ciclo: ciclo mas interno.
5. Responde con una prueba de escritorio RAM: estado inicial, recorrido,
   cambios en variables/arreglos/memoria, salida o decisiones y una idea clave.

`@ver` si puede usar nombres reales de variables y funciones, porque su objetivo
es visualizar el codigo concreto. No debe reescribir el programa si el usuario
solo pidio entenderlo.

## Protocolo `@sintetizar`

Cuando el usuario escriba `@sintetizar`, `sintetizar` o "resume la sesion":

1. Lee `.agent/skills/sintetizar/SKILL.md`.
2. Analiza los commits y logs de la sesion.
3. Actualiza `usuarios/<slug>/errores.md` sin borrar entradas existentes. Si no
   existe, inicializalo desde `errores.template.md` antes de escribir.
4. Reporta los patrones documentados.

## Protocolo `@test` / `@validar`

Cuando el usuario escriba `@test`, `@validar`, "crea tests", "valida mi
solucion" o algo equivalente para un ejercicio que no sea Exercism:

1. Lee `.agent/skills/test/SKILL.md`.
2. Lee el `.c` activo, el `README.md` visible o
   `.estudio-exercism/support/README.md`, y `.estudio-exercism.json`.
3. Si el proveedor es Exercism, no generes tests: usa los tests oficiales.
4. Para PDF Alejandro, W3Schools o w3resource, genera tests en
   `.estudio-tests/` siguiendo el layout de la skill.
5. No cambies el codigo del estudiante ni escribas la solucion.
6. Explica solo el alcance de los tests y como ejecutarlos.

Un ejercicio no-Exercism solo debe marcarse como completado cuando la validacion
local de `.estudio-tests` termine con codigo 0.

## Reglas De Edicion

- No reemplaces el metodo socratico por soluciones completas.
- No escribas codigo C como respuesta a una pista, salvo que el usuario pida
  explicitamente salir del modo socratico.
- Si editas archivos del sistema, manten el estilo simple de Markdown y scripts
  Windows.
- Respeta cambios existentes del estudiante, especialmente en `Ejercicios/`,
  salvo que el usuario haya pedido una limpieza o una version base del framework.
