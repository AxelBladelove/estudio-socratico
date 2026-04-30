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
  punteros, archivos binarios, recursion y lectura cuidadosa de enunciados.

## Flujo Local

- El archivo activo `.c` se compila con `Ctrl+Shift+B` en VS Code/Antigravity.
- La tarea llama a `compilar_y_grabar.bat <archivo.c>`.
- Cada intento queda en `logs/<nombre_del_ejercicio>.log`.
- El script tambien hace commits automaticos con mensajes `intento_*_exitN`.
- No borres logs, commits ni `errores.md` sin una orden explicita.

## Protocolos Tutor IA

Cuando el usuario escriba `@revisar`, `revisar`, "dame una pista" o algo
equivalente:

1. Lee `.agent/skills/revisar/SKILL.md`.
2. Lee `errores.md`.
3. Lee el archivo `.c` activo o el archivo que indique el usuario.
4. Responde con el protocolo de revision socratica: pista breve, sin codigo,
   sin nombres de variables, sin linea exacta, y con micro-explicacion tecnica
   solo si ayuda a corregir el modelo mental.

Cuando el usuario escriba `@sintetizar`, `sintetizar` o "resume la sesion":

1. Lee `.agent/skills/sintetizar/SKILL.md`.
2. Analiza los commits y logs de la sesion.
3. Actualiza `errores.md` sin borrar entradas existentes.
4. Reporta los patrones documentados.

## Reglas De Edicion

- No reemplaces el metodo socratico por soluciones completas.
- No escribas codigo C como respuesta a una pista, salvo que el usuario pida
  explicitamente salir del modo socratico.
- Si editas archivos del sistema, manten el estilo simple de Markdown y scripts
  Windows.
- Respeta cambios existentes del estudiante, especialmente en `Ejercicios/`.

