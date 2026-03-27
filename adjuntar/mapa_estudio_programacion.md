# 💻 Mapa de Estudio — Programación (38 días)
**Fuentes:** Exercism C Track (84 ejercicios) + PDF Batista/Liz  
**Objetivo:** Dominar el material al nivel del examen del Prof. Alejandro Liz  
**Periodo:** 21 marzo → 28 abril 2026

---

## CÓMO LEER ESTE DOCUMENTO

Cada sesión tiene dos elementos:
- **Exercism** → el ejercicio que abres en exercism.org y resuelves con TDD (tests automáticos)
- **PDF** → el ejercicio del libro que resuelves en CodeBlocks después, si terminas el de Exercism

El Shortcut te crea ambas tareas en Reminders automáticamente. Tú no decides nada — solo abres la tarea y la haces.

---

## SEMANA 1 — Ciclos, Condiciones, Funciones
*23–29 marzo | Concepto central: producir código C sin titubear*

### Lunes 23 marzo *(bloque 6pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 1 | Exercism | `hello-world` | Setup del workflow. Tests verdes = hábito correcto |
| 2 | Exercism | `leap` | if/else, lógica booleana |
| 3 | PDF Sección 1 | #1 Imprimir nombre N veces | Primer `for` propio |

### Martes 24 marzo *(bloque 6:30pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 4 | Exercism | `difference-of-squares` | Loops, funciones matemáticas |
| 5 | PDF Sección 1 | #3 Suma de 0 a N | Acumulador en loop |

### Miércoles 25 marzo *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 6 | Exercism | `collatz-conjecture` | While loop, funciones con retorno |
| 7 | Exercism | `grains` | Operadores bit a bit, potencias |
| 8 | PDF Sección 1 | #5 Impares < 20 ascendente | Loop con condición |
| 9 | PDF Sección 1 | #6 Impares < 20 descendente | Loop descendente |

### Jueves 26 marzo *(bloque 5:45pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 10 | Exercism | `space-age` | Funciones con parámetros, retorno float |
| 11 | PDF Sección 1 | #7 Función `maxval(n,m)` | Primera función propia con retorno |
| 12 | PDF Sección 1 | #10 Tabla Fahrenheit → Celsius | Tabla con formato, float |

### Viernes 28 marzo *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 13 | Exercism | `darts` | Structs básicos, funciones matemáticas |
| 14 | Exercism | `two-fer` | Strings con snprintf, condicional en string |
| 15 | PDF Sección 1 | #11 Divisores exactos de N | Loop con módulo + validación entrada |

### Sábado 29 marzo
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 16 | Exercism | `hamming` | Strings como arrays de char, loops |
| 17 | PDF Sección 1 | #14 Libreta de ahorros | Interés compuesto, loop acumulativo |
| 18 | PDF Sección 1 | #17 Suma/cuadrados/promedio/max/min | 5 cálculos en un solo loop |

### Domingo 30 marzo
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 19 | Exercism | `raindrops` | Condicionales múltiples, strings |
| 20 | PDF Sección 1 | #22 Función `pi(n)` — serie Leibniz | Serie matemática, acumulador float |
| 21 | PDF Sección 1 | #25 `potencia(b,e)` | Multiplicaciones sucesivas, función reutilizable |

**✅ Criterio de cierre Semana 1:** Puedes escribir cualquier función con parámetros y retorno sin mirar referencias. Los loops `for` y `while` son automáticos.

---

## SEMANA 2 — Arrays, Punteros, Strings
*30 marzo – 5 abril | Concepto central: la memoria tiene direcciones, los punteros son esas direcciones*

### Lunes 30 marzo *(bloque 6pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 22 | Exercism | `resistor-color` | Arrays, enums, índices |
| 23 | Exercism | `resistor-color-duo` | Arrays + aritmética de índices |
| 24 | PDF Sección 2 | #4 Temperaturas por hora | Array de 24 elementos, max/min/promedio |

### Martes 31 marzo *(bloque 6:30pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 25 | Exercism | `resistor-color-trio` | Arrays + structs juntos |
| 26 | PDF Sección 2 | #1 `swap(int *a, int *b)` | **Primer puntero real.** La dirección `&a` vs el valor `a` |

### Miércoles 1 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 27 | Exercism | `high-scores` | Arrays dinámicos, búsqueda de máximo |
| 28 | Exercism | `rna-transcription` | Strings como arrays de char, switch |
| 29 | PDF Sección 2 | #2 `encender(n,b)` | Puntero + operación bit a bit |
| 30 | PDF Sección 2 | #6 Suma de dos arreglos | Arrays como parámetros, tamaño N |

### Jueves 2 abril *(bloque 5:45pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 31 | Exercism | `isogram` | Strings, arrays de bool para tracking |
| 32 | PDF Sección 3 | #2 Función a mayúsculas | Manipulación char a char, ASCII |

### Viernes 3 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 33 | Exercism | `pangram` | Strings + frecuencia de caracteres |
| 34 | Exercism | `reverse-string` | Reverse con manejo de memoria |
| 35 | PDF Sección 3 | #4 `contarc(s,c)` | Recorrer string buscando caracter |

### Sábado 4 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 36 | Exercism | `nucleotide-count` | Strings + conteo en array |
| 37 | PDF Sección 3 | #13 `rev(s)` | Invertir string in-place con punteros |
| 38 | PDF Sección 3 | #12 `numpal(s)` | Contar palabras con punteros |

### Domingo 5 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 39 | PDF Sección 3 | #1 Versión propia de `strlen` y `strcpy` | Entender cómo funcionan por dentro |
| 40 | 🔁 SIMULACRO | Parcial 1 de Damarys (en papel, 45 min) | Replicar condición de examen |

**✅ Criterio de cierre Semana 2:** Puedes escribir una función que recibe un array por puntero, lo recorre, y retorna un resultado. El concepto `*` vs `&` no te genera duda.

---

## SEMANA 3 — Structs + Recursión
*6–12 abril | Concepto central: modelar datos complejos + pensar recursivamente*

> Esta semana es la más importante. El examen de Alejandro vive aquí.

### Lunes 6 abril *(bloque 6pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 41 | Exercism | `robot-simulator` | **Struct completo con funciones que lo modifican** |
| 42 | PDF Sección 4 | #1 `fact(n)` recursivo | Primera función recursiva. Caso base + caso recursivo |

### Martes 7 abril *(bloque 6:30pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 43 | Exercism | `triangle` | Struct + lógica de validación |
| 44 | PDF Sección 4 | #2 Fibonacci recursivo | Recursión con dos llamadas |

### Miércoles 8 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 45 | Exercism | `allergies` | Struct + bitwise operations + arrays |
| 46 | Exercism | `complex-numbers` | Struct con operaciones matemáticas |
| 47 | PDF Sección 4 | #7 `potencia(b,e)` recursiva | Misma función de Semana 1 pero recursiva |
| 48 | PDF Sección 5 | #4 Aritmética de complejos con struct | Implementación propia del struct |

### Jueves 9 abril *(bloque 5:45pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 49 | Exercism | `yacht` | Array dentro de struct, múltiples funciones |
| 50 | PDF Sección 4 | #9 MCD recursivo con módulo | Algoritmo de Euclides |

### Viernes 10 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 51 | Exercism | `clock` | Struct con operaciones, formato de string |
| 52 | PDF Sección 5 | #1 Hora decimal → struct hora:min:seg | Struct como parámetro de función |
| 53 | PDF Sección 4 | #8 `printbin(n)` recursiva | Recursión + bits |

### Sábado 11 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 54 | Exercism | `rational-numbers` | Struct + 4 operaciones + simplificación |
| 55 | PDF Sección 5 | #7 `pendiente(RECTA r)` | Struct como parámetro, retorno float |

### Domingo 12 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 56 | Exercism | `grade-school` | **Array de structs + sorting** — aquí empieza el nivel de Alejandro |
| 57 | 🔁 SIMULACRO | Diseña tu propio `typedef struct CANCION` con 5 campos y 3 funciones | Réplica directa del estilo del 2do parcial |

**✅ Criterio de cierre Semana 3:** Puedes leer el `typedef struct` del 2do parcial de Alejandro y entender en 3 minutos qué estructura de datos está modelando.

---

## SEMANA 4 — Memoria Dinámica + Archivos
*13–19 abril | Concepto central: malloc/free + I/O de archivos binarios*

> Esta semana replica exactamente las preguntas del 2do parcial de Alejandro.

### Lunes 13 abril *(bloque 6pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 58 | Exercism | `circular-buffer` | **malloc + free + struct con memoria dinámica** |
| 59 | PDF Sección 6 | #1 Contar líneas de archivo | `fopen`, `fgets`, `fclose` — I/O básico |

### Martes 14 abril *(bloque 6:30pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 60 | Exercism | `list-ops` | Array dinámico, function pointers, structs — nivel alto |
| 61 | PDF Sección 6 | #2 Frecuencia de letras | `fgetc`, loop sobre archivo completo |

### Miércoles 15 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 62 | Exercism | `spiral-matrix` | Matriz dinámica con malloc, índices 2D |
| 63 | 🔑 CLAVE | Crear array dinámico de `ARTISTA` con malloc | Exactamente la `dropartista()` del parcial de Alejandro |
| 64 | 🔑 CLAVE | Archivo binario: escribir y leer array de structs con `fwrite`/`fread` | Exactamente el `historial.dat` del parcial |

### Jueves 16 abril *(bloque 5:45pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 65 | PDF Sección 6 | #4 `cola.c` — últimas 10 líneas | `fseek`, `ftell` — navegar en archivo |
| 66 | Exercism | `saddle-points` | Structs con arrays dinámicos |

### Viernes 17 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 67 | 🔁 SIMULACRO COMPLETO | **2do Parcial de Alejandro** (el real, condiciones de examen, 90 min) | El momento de la verdad |
| 68 | Post-simulacro | Corrección con Gemini Socrático: identificar cada error, entender por qué | No ver respuestas — hacer preguntas |

### Sábado 18 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 69 | PDF Sección 5 | #5 10 estudiantes / 5 prácticas | Array de structs, estadísticas |
| 70 | 🔁 SIMULACRO | Problema estilo Alejandro: struct PLAYLIST con funciones recursivas | Generado por Gemini, resuelto solo |

### Domingo 19 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 71 | Corrección | Post-simulacro con Gemini Socrático | |
| 72 | PDF Parte II | #5 Blackjack (análisis + diseño) | Structs + arrays + lógica — minijuego completo |

**✅ Criterio de cierre Semana 4:** Puedes implementar `dropartista()` del 2do parcial desde cero en menos de 45 minutos.

---

## SEMANA 5 — Integración Total + Modo Examen
*20–27 abril | Concepto central: velocidad + precisión + código limpio bajo presión*

### Lunes 20 abril *(bloque 6pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 73 | PDF Parte II | #5 Blackjack (implementación) | El ejercicio más parecido al estilo Alejandro |

### Martes 21 abril *(bloque 6:30pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 74 | PDF Sección 1 (avanzado) | #35 Factorización en primos | Algoritmo clásico, loops anidados |
| 75 | Exercism | `prime-factors` | Mismo concepto, con tests |

### Miércoles 22 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 76 | 🔁 SIMULACRO | Parcial inventado estilo Alejandro (structs + funciones + archivo) | Generado por Gemini |
| 77 | Corrección | Post-simulacro | |

### Jueves 23 abril *(bloque 5:45pm–8:15pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 78 | 🔁 SIMULACRO FINAL | Problema nuevo completo — sin referencias, 90 min | Simulación real del primer parcial |
| 79 | Corrección profunda | Revisar cada línea con Gemini Socrático | |

### Viernes 24 abril *(bloque 11am–2pm)*
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 80 | Repaso | Reescribir desde cero los 3 ejercicios donde más fallaste en simulacros | Memoria muscular final |

### Sábado 25 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 81 | Repaso | Ciclos, funciones, arrays — los básicos rápido | Semanas 1–2 consolidadas |
| 82 | Repaso | Releer el PDF de Alejandro — esta vez entenderás todo | |

### Domingo 26 abril
| Tarea | Fuente | Ejercicio | Concepto |
|-------|--------|-----------|---------|
| 83 | Repaso | Structs, memoria dinámica, archivos | Semanas 3–4 consolidadas |

### Lunes 27 abril
| Tarea | Actividad |
|-------|-----------|
| 84 | Leer el syllabus completo. Verificar que no haya vacíos. Dormir bien. |

### Martes 28 abril 🎓
**Inicia el cuatrimestre con el Prof. Alejandro Liz.**

---

## RESUMEN DE EJERCICIOS TOTALES

| Semana | Exercism | PDF/Simulacros | Total tareas |
|--------|----------|----------------|-------------|
| 1 | 7 | 9 | 16 |
| 2 | 7 | 8 + 1 simulacro | 16 |
| 3 | 7 | 7 + 1 simulacro | 15 |
| 4 | 5 | 6 + 2 simulacros | 13 |
| 5 | 2 | 5 + 3 simulacros | 10 |
| **Total** | **28** | **35 + 7 simulacros** | **70** |

---

## LO QUE EL SHORTCUT NECESITA SABER

Para generar estas tareas automáticamente en Reminders, el Shortcut tiene la lista completa arriba y sabe:
- Qué día de la semana corresponde a cada número de sesión
- Cuántas tareas caben por bloque según el horario
- Que las tareas de Exercism van a la lista `{} 💻 C.S. & Programación`
- Que los simulacros llevan una etiqueta especial 🔁
- Que las tareas 🔑 CLAVE son inamovibles — no se pueden reagendar

---

*Última actualización: 21 de marzo de 2026*
