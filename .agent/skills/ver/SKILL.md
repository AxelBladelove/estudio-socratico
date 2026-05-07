---
name: ver
description: >
  Prueba de escritorio RAM para codigo C. Usa el archivo .c activo y una linea
  del editor para explicar paso a paso que hace una funcion, main, un ciclo o
  todo el archivo, mostrando como cambian variables, memoria, decisiones y
  salida.
---

# Skill: Ver

## Proposito

`@ver` no es una pista. Es una herramienta de visualizacion.

Tu objetivo es ayudar al estudiante a ver que hace su programa en ejecucion:
que variables existen, que valores cambian, que decisiones toma el flujo y que
salida produce. La explicacion debe parecer una prueba de escritorio de RAM,
no una correccion del ejercicio.

## Entrada Necesaria

Intenta obtener:

1. archivo `.c` activo o indicado por el estudiante;
2. linea actual del editor o linea escrita por el estudiante;
3. si el codigo usa entrada por teclado, un caso de prueba pequeno.

Si falta la linea y no puedes inferirla, pide una sola cosa:

> Dime el numero de linea o coloca el cursor en la zona que quieres ver.

Si falta un dato de entrada, elige un ejemplo pequeno y dilo claramente. Si el
ejemplo podria cambiar la conclusion, pide el dato.

## Como Elegir El Alcance

Usa la linea indicada para decidir que explicar.

- Si la linea es `1`, explica el archivo completo del ejercicio: funciones,
  flujo general y una traza representativa.
- Si la linea coincide con `main` o cae dentro de `main`, explica todo `main`,
  salvo que este dentro de un ciclo mas especifico y el estudiante haya pedido
  ese ciclo.
- Si la linea coincide con una funcion, explica toda esa funcion.
- Si la linea coincide con un `for`, `while` o `do`, explica ese ciclo completo.
- Si la linea cae dentro de un ciclo, usa el ciclo mas interno.
- Si la linea cae dentro de otra funcion, explica la funcion completa.
- Si la linea apunta a una instruccion normal, declaracion, asignacion,
  llamada, operacion con punteros, acceso a arreglo, acceso a `struct`,
  operacion de archivo o expresion parecida, explica la unidad logica que
  empieza en el primer texto real de esa linea y termina cuando termina la
  intencion de esa instruccion.

Cuando haya duda entre una funcion y un ciclo, elige el bloque mas pequeno que
explique bien lo que el estudiante senalo.

## Lineas Sueltas Y Unidad Logica

Una linea no siempre representa una funcion o un ciclo. A veces el estudiante
quiere entender una sola accion de C, por ejemplo una declaracion de puntero, una
reserva de memoria, una asignacion, una comparacion, una llamada a funcion o un
acceso a un arreglo.

En esos casos:

1. Empieza en el primer caracter no blanco de la linea indicada.
2. Sigue hasta el final natural de la logica:
   - el `;` de la instruccion;
   - el cierre de una expresion partida en varias lineas;
   - el bloque pequeno que pertenece a esa condicion o llamada;
   - o la mini-secuencia inmediata si la linea solo se entiende con su
     comprobacion, uso o liberacion cercana.
3. No subas automaticamente a toda la funcion si basta con explicar esa unidad.
4. Si necesitas una linea anterior o posterior para que la explicacion sea
   honesta, incluyela y dilo: "incluyo esta linea porque le da sentido al
   puntero" o "incluyo esta comprobacion porque protege el acceso".

Ejemplos de unidad logica:

- Una declaracion de puntero: explica que variable nace, que direccion guarda y
  si apunta a algo valido.
- Una desreferenciacion: explica que direccion se intenta abrir y que valor se
  lee o modifica.
- Una llamada a funcion: explica que argumentos se entregan, si son copias o
  direcciones, y que efecto vuelve.
- Una expresion de arreglo: explica que indice se calcula y que casilla se toca.
- Un `malloc` o `fopen`: explica el recurso pedido, el valor devuelto y la
  comprobacion necesaria antes de usarlo.

## Como Analizar El Codigo

Antes de responder:

1. Lee el archivo completo, no solo el bloque.
2. Identifica variables locales, parametros, arreglos, structs, punteros y
   funciones auxiliares que afecten el bloque.
3. Si explicas una funcion, busca desde donde se llama para entender valores
   probables.
4. Si hay `scanf`, `rand`, archivos o valores externos, usa un caso concreto y
   marca lo que depende de ese dato.
5. Si hay comportamiento indefinido o memoria no inicializada, dilo como parte
   de la traza: "aqui la RAM no tiene un valor confiable".

## Forma De La Respuesta

Usa una estructura clara:

1. **Alcance:** que bloque estas mirando.
2. **Estado inicial:** variables importantes antes de empezar.
3. **Recorrido:** pasos de ejecucion.
4. **RAM / cambios:** como cambian valores o estructuras.
5. **Salida o decision:** que se imprime, retorna o decide.
6. **Idea clave:** una conclusion corta para que el estudiante pueda repetir la
   traza solo.

Para el recorrido, usa una tabla cuando ayude:

| Paso | Que ejecuta | Que cambia en RAM | Resultado |
|---|---|---|---|

No hace falta explicar cada llave, `#include` o detalle mecanico si no cambia el
estado mental del estudiante. Si una linea solo abre un bloque, agrupa la
explicacion con la condicion o cuerpo correspondiente.

## Limites

- No reescribas el codigo.
- No propongas una solucion completa si el estudiante solo pidio ver.
- No conviertas la respuesta en una lista de todos los errores del programa.
- Si encuentras un bug, puedes marcarlo como "observacion", pero mantente en la
  ejecucion: que pasaria y por que.
- Puedes usar nombres reales de variables y funciones. Esta skill existe para
  visualizar el codigo concreto.

## Nivel De Detalle

El estudiante esta aprendiendo C. Explica conceptos que suelen ser invisibles:

- una variable local nace al entrar a su bloque;
- un arreglo reserva varias casillas contiguas;
- un indice no es el dato, es la posicion;
- un acumulador guarda historia;
- un contador mide cantidad;
- un puntero guarda una direccion;
- una funcion recibe copias salvo que reciba direcciones;
- `printf` avanza el cursor segun caracteres impresos;
- una condicion decide si el flujo entra, repite o salta.

## Ejemplo De Tono

> **Alcance:** voy a mirar el ciclo que recorre el arreglo.
>
> Antes de entrar, el arreglo ya tiene valores en sus primeras posiciones y el
> contador indica cuantas de esas posiciones son validas. En la primera vuelta,
> el indice apunta a la casilla 0; se lee ese valor, se compara con la condicion
> y solo si pasa se actualiza el acumulador. La parte importante es que el indice
> cambia para moverse por casillas, pero el acumulador cambia para guardar una
> historia.
