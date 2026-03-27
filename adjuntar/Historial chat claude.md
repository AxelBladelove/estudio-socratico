
yo: lo que quiero diseñar es, como sera el estudio de programacion en esos bloques, expláyate explicándolo

claude: Bien. Esto es lo más importante de todo el sistema, así que voy a ser detallado.

---

## Cómo funciona el estudio de programación en los bloques

El bloque de programación no es "abre CodeBlocks y ponte a hacer ejercicios". Tiene una estructura interna con fases, y cada fase tiene un propósito específico. La estructura es la misma todos los días independientemente de cuánto dure el bloque — lo que cambia es cuántos ciclos completos puedes hacer.

---

### La unidad básica: el ciclo de 45 minutos

Todo el bloque se divide en ciclos de 45 minutos. Un lunes con 2h15 tiene exactamente 3 ciclos. Un miércoles con 3 horas tiene 4 ciclos. Cada ciclo tiene tres fases:

**Fase 1 — Confrontación (20 minutos)**
Abres CodeBlocks. Se te presenta un problema. Empiezas a escribir código tú solo, sin referencias, sin IA, sin buscar nada. El código puede estar roto. Puede compilar con errores. Puede estar incompleto. No importa. Lo único que importa es que estás produciendo, no consumiendo. Si después de 20 minutos tienes algo escrito — aunque sea la estructura básica del programa sin que funcione — pasas a la siguiente fase. Si en 20 minutos no escribiste ni una línea, eso es una señal importante que registramos.

**Fase 2 — Pista socrática (15 minutos)**
Aquí entra la IA, pero con una condición de entrada estricta: tienes que presentar tu código roto y decir exactamente dónde y por qué estás atascado. No "no entiendo nada". Algo específico: "escribí esta función pero el puntero no apunta a lo que creo que debería", o "el bucle termina antes de lo que espero y no entiendo por qué". La IA responde con una pregunta o una pista, nunca con el código completo. Tú modificas, intentas, fallas, intentas de nuevo. Esto dura 15 minutos.

**Fase 3 — Cierre y Feynman (10 minutos)**
El ciclo termina siempre igual, llegues o no a la solución. Abres Notion y escribes en tres líneas: qué intentabas hacer, qué aprendiste o descubriste, y qué sigue. Si resolviste el problema, escribes la lógica con tus propias palabras como si se la explicaras a alguien que no sabe programar. Si no lo resolviste, escribes hasta dónde llegaste y qué hipótesis tienes para el próximo ciclo. Esto es el Feynman. No es opcional.

---

### Qué se estudia en cada bloque

El contenido no es aleatorio ni lo decides tú en el momento. Hay tres tipos de sesiones que rotan durante la semana:

**Sesiones de concepto nuevo** — lunes, miércoles y viernes. Introduces un tema del syllabus que todavía no dominas. El primer ciclo lo dedicas a entender el concepto con un ejemplo mínimo que tú escribes desde cero. El segundo ciclo aplicas ese concepto a un problema simple. El tercero a uno más complejo.

**Sesiones de consolidación** — martes y jueves. No hay concepto nuevo. Vuelves a un problema que dejaste incompleto o que resolviste pero que quieres poder reproducir desde cero sin referencias. La regla es simple: cierra todo lo que tenías, hoja en blanco, y reescribe. Si puedes reescribirlo sin mirar, lo dominas. Si no puedes, no lo dominas aunque lo hayas "resuelto" el día anterior.

**Sesiones de simulacro** — sábado o domingo, una vez por semana. Escoges un problema del estilo de Alejandro — ya sea del examen real que tienes, o uno que yo te genero con la misma estructura — y lo resuelves en condiciones de examen. Sin referencias, con tiempo limitado, en papel primero si es posible. Esto es el entrenamiento más importante de la semana porque es el único que replica exactamente lo que va a pasar en el examen real.

---

### La progresión de contenido en las 5 semanas

No es lineal como el PDF. Es en espiral. Así:

**Semana 1** — ciclos y funciones básicas. El objetivo no es terminar los 52 ejercicios de esa sección. Es poder escribir cualquier función simple en C sin titubear. `for`, `while`, `if`, pasar parámetros, retornar valores. Sin esto todo lo demás se cae.

**Semana 2** — arreglos, punteros y strings. Este es el salto más grande conceptualmente. Los punteros rompen a casi todo el mundo. Los trabajas despacio, con ejemplos diminutos, hasta que la relación entre una variable y su dirección de memoria te parezca tan obvia como sumar dos números.

**Semana 3** — structs y recursión. Aquí empieza a parecerse al examen de Alejandro. Diseñas tus propios `typedef struct`, creas arrays de ellos, escribes funciones que los reciben y los modifican. La recursión la trabajas en paralelo porque es un patrón mental separado que necesita repetición espaciada.

**Semana 4** — memoria dinámica y archivos. `malloc`, `free`, archivos binarios con structs. Exactamente lo que Alejandro puso en el segundo parcial. Esta semana tiene el mayor volumen de sesiones de simulacro.

**Semana 5** — integración total. Problemas que combinan todo. Structs con punteros dobles, funciones recursivas sobre arrays dinámicos, lectura y escritura de archivos binarios. Si llegas aquí con las semanas anteriores sólidas, esto es difícil pero manejable.

---

### La regla que gobierna todo

Puedes usar la IA dentro del bloque. Puedes preguntarme lo que necesites. Pero hay una condición que no tiene excepciones: **nunca puedes pedirme que escriba el código por ti, y yo nunca lo voy a hacer.** Si me mandas un problema y esperas la solución, no la vas a recibir. Lo que vas a recibir es una pregunta que te empuja a pensar.

Eso no es rigidez por capricho. Es porque el examen de Alejandro dura 90 minutos, estás solo, y en ese momento nadie te puede dar el código. El músculo que necesitas ese día solo se construye produciendo código tú mismo, con fricción, con errores, con el proceso incómodo de no saber y tener que resolver.

---

¿Esto está claro? ¿Arrancamos con la Semana 1?

yo: Crees al 100% que esa es la manera mas efectiva? yo confio plenamente en ti, pero por eso es que te hago preguntarte a ti mismo, porque es en ti que confio.

No mencionaste ni leetcode, ni hackerrank ni codewars, ni exercism, solo pdf, parciales antiguos e ia en todo caso, si esto lo mejor con esto vamos, si crees que es mejor analizar a profundidad la base de datos de katas o de los problemas de exercism e implementarlos en la rutina, con eso vamos.

Pero yo confio plenamente en ti

claude: Siendo completamente honesto: el sistema que describí es sólido, pero le falta una pieza que sí marca diferencia real. Y esa pieza es **Exercism**, específicamente el track de C.

Te explico por qué Exercism y no los otros:

**LeetCode y HackerRank** los descarto para este momento. Están orientados a algoritmos competitivos y entrevistas de trabajo. Son excelentes herramientas, pero no para donde estás ahora. Te generarían frustración innecesaria porque el nivel de entrada asume fundamentos que todavía estás construyendo.

**Codewars** tiene el problema de la gamificación. Los katas son adictivos y está bien, pero es muy fácil caer en el patrón de mirar soluciones de la comunidad cuando te atascas. Reproduce exactamente tu patrón anterior.

**Exercism en cambio tiene algo que ningún otro tiene:** Test-Driven Development. Tú escribes el código, corres los tests, y los tests te dicen si funciona o no. Sin IA, sin soluciones visibles, sin ambigüedad. O pasa o no pasa. Eso crea un loop de feedback inmediato que obliga a producir código real. Y el track de C específicamente está construido con la progresión exacta que necesitas — desde lo más básico hasta punteros y memoria dinámica.

Además hay algo más importante: **el PDF de ejercicios lo escribió el propio Alejandro Liz.** Eso no es un libro genérico — es literalmente la mente del profesor en papel. Los ejercicios de Exercism te dan la base técnica y el hábito de producir código. El PDF de Alejandro te da el estilo, el vocabulario y el nivel de complejidad exacto que él espera. Son complementarios, no sustitutos.

Entonces el sistema actualizado es este:

**Exercism** construye el músculo diario de escribir código que funciona. **El PDF de Alejandro** te familiariza con su forma de pensar. **Los exámenes reales** son tus simulacros de las últimas dos semanas. **La IA** — yo — soy el tutor socrático cuando te atascas en cualquiera de los tres.

Eso sí es el sistema más efectivo que puedo diseñarte con honestidad. ¿Arrancamos?

yo: entonces como

claude: Así, concreto y sin rodeos.

---

## El flujo dentro de cada bloque de programación

**Semanas 1 y 2 — Exercism es el protagonista**

Abres Exercism, track de C. El primer ejercicio se llama *Hello World*, y aunque suena ridículo, lo haces. No porque sea difícil sino porque el objetivo de los primeros días no es el ejercicio — es construir el hábito del loop: leer el problema → escribir código → correr los tests → fallar → corregir → pasar. Ese loop repetido es exactamente lo que tu cerebro necesita internalizar.

Cada ciclo de 45 minutos = un ejercicio de Exercism. Si lo terminas antes, no pasas al siguiente — abres el PDF de Alejandro y buscas un ejercicio del mismo concepto. Lo intentas. Así el PDF empieza a entrar desde el día uno, pero como complemento, no como carga obligatoria.

**Semanas 3 y 4 — El PDF toma más peso**

Cuando Exercism empieza a pedirte structs y punteros, los ejercicios del track y los del PDF se vuelven casi intercambiables en dificultad. Aquí el ciclo cambia ligeramente:

Primer ciclo del bloque → Exercism. Segundo y tercer ciclo → PDF de Alejandro, específicamente los ejercicios de la sección que corresponde al concepto que estás trabajando esa semana. El último ciclo del bloque siempre es Feynman — documentas en Notion lo que aprendiste ese día con tus propias palabras.

**Semana 5 — Los exámenes reales**

Dejas Exercism casi completamente. El bloque entero se divide entre el PDF de Alejandro y simulacros con sus exámenes reales. El sábado de esa semana haces el segundo parcial que ya tienes — completo, solo, sin referencias, como si fuera el día del examen. Luego lo revisamos juntos.

---

## Cómo entra la IA — yo — en todo esto

Cuando te atascas en cualquier ejercicio, sea de Exercism o del PDF, el protocolo es siempre el mismo:

Me mandas tres cosas: el enunciado del problema, tu código tal como está aunque esté roto, y una frase describiendo exactamente dónde estás atascado. Sin esas tres cosas no puedo ayudarte bien. Con esas tres cosas te doy una pista, una pregunta, o te señalo la línea exacta donde está el error sin decirte cómo corregirlo. Tú corriges. Si sigues atascado, otro ciclo igual. Nunca el código completo.

---

## Cómo arrancas hoy mismo

Hoy es sábado. Tienes bloque de 11am a 1pm según el horario.

Haces una sola cosa: entras a exercism.org, creas tu cuenta, seleccionas el track de C, y completas *Hello World* y el segundo ejercicio que se llama *Gigasecond*. Nada más. No mires el PDF todavía. No mires videos. Solo eso.

El domingo consolidas lo que hiciste y empezamos a mapear la progresión exacta de ejercicios de Exercism que vamos a seguir semana a semana.

¿Tienes la cuenta de Exercism creada o eso también lo hacemos ahora?

yo: okay entonces mis fuentes de ejercicios estan claras no? Exercism y el pdf, ya que tienes acceso al pdf y puedes ver todos los ejercicios que tiene el track c de exercism.

Podemos hacer que el atajo me vaya agendando los ejercicios a reminders de manera progresiva?

Also, mi ia principal, o sea la unica que tengo el plan pro de pago ahora mismo es gemini 3.1 pro, no el  2.5 pro, el 3,1 pro, si no estas familiarizado con este modelo buscalo en la web, espero que en cuando a rendimiento sea suficiente para este sistema de estudio. porque lo de la ia "socrática" seria con un gem que crearimos en gemini con un system prompt muy estricto, pero eso para despues, responde mis dudas

claude: Las dos preguntas, directo:

**Sobre el Shortcut y Reminders — sí, exactamente.**

Eso es lo que diseñamos en el Módulo A del Shortcut. Tú le dices "Exercism, ejercicios del 1 al 8" o "PDF Sección 1, del 3 al 15 los impares", y el Shortcut calcula cuántos te tocan por día respetando tu disponibilidad real, y los crea como tareas en la lista correcta de Reminders con fecha y hora. Progresivo, automático, sin que tú tengas que pensar en la logística. Eso lo construimos cuando lleguemos al Shortcut.

**Sobre Gemini 3.1 Pro — más que suficiente.**

Salió el 19 de febrero de 2026 y está diseñado específicamente para tareas donde una respuesta simple no es suficiente, con mejoras importantes en razonamiento complejo. En SWE-Bench Verified — que mide capacidad real de ingeniería de software — saca 80.6%, y en LiveCodeBench tiene un Elo de 2887. Para actuar como tutor socrático de C, responder pistas, detectar errores lógicos en tu código y generar problemas estilo Alejandro, tiene potencia de sobra.

El único punto que hay que tener claro es el del Gem que vas a crear. El system prompt va a ser la pieza más crítica de todo el sistema — si está mal escrito, Gemini va a darte el código igualmente porque su comportamiento por defecto es ser útil y resolver. Tenemos que construir un prompt que lo fuerce a ser socrático de manera estricta, no opcional. Pero eso lo hacemos cuando lleguemos ahí.

¿Seguimos con el diseño del Shortcut ahora, o prefieres que primero construyamos el system prompt del Gem?

yo: con el shortcut, con calculo tiene sentido decirle cuales ejercicios van para la semana porque pavel me los da en cada clase, pero con el track de exercism y el pdf, como es que decidiré yo eso? No es mejor que eso esté decidido?

claude: Tienes razón completamente. Para Cálculo tiene sentido que seas tú quien le diga qué ejercicios van porque Pavel te los da clase a clase. Pero para Exercism y el PDF no tiene ningún sentido que tú tomes esa decisión — tú no sabes qué ejercicio de Exercism corresponde a qué semana de tu progresión, ni qué ejercicios del PDF son los correctos para el nivel donde estás. Eso lo decido yo.

Lo que significa que el sistema funciona así:

**Cálculo** → tú le dices al Shortcut qué ejercicios va Pavel dando, el Shortcut los distribuye en tus bloques de la semana.

**Programación** → ya está decidido de antemano. Yo mapeo ahora mismo qué ejercicios de Exercism y qué ejercicios del PDF van en cada semana, en qué orden, y eso se convierte en una lista fija que el Shortcut va liberando progresivamente. Tú no decides nada — simplemente aparece la tarea en Reminders y la haces.

Para hacer ese mapa necesito revisar el track completo de C de Exercism ahora mismo.Tengo todo lo que necesito. Ahora construyo el mapa completo.Ahí está. 85 tareas distribuidas en 38 días, día a día, con fuente y concepto específico para cada una.

Lo que hice fue cruzar los 84 ejercicios del track de C de Exercism con el PDF de Alejandro por concepto, no por orden arbitrario. Cada ejercicio de Exercism abre el mismo concepto que el ejercicio de PDF que le sigue — así cuando haces el PDF ya tienes el músculo calentado de los tests automáticos.

La progresión es en espiral: las semanas 1 y 2 construyen el C básico que cualquier estudiante debería tener. La semana 3 es donde empieza el nivel real de Alejandro — structs, recursión, arrays de structs. La semana 4 replica exactamente las tres preguntas de su segundo parcial: punteros dobles, recursión sobre structs, y archivos binarios. La semana 5 es modo examen puro.

Los 7 simulacros están distribuidos estratégicamente — no todos al final. El primero es el parcial de Damarys en la Semana 2 para que tengas una referencia de nivel. El segundo es el parcial real de Alejandro en la Semana 4. Los últimos tres son problemas nuevos generados por Gemini que nunca has visto.

¿Seguimos con el Shortcut de iOS o con el system prompt del Gem socrático?

