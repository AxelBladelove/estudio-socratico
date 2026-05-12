# Prompt maestro: conversión del PDF de Alejandro a ejercicios estilo Exercism

## Objetivo

Convertir el PDF `Problemas de programacion.pdf` en un catálogo de ejercicios para la extensión de Estudio Socrático, usando un formato parecido a Exercism:

```text
usuario ve un título claro
↓
hace clic
↓
se crea una carpeta del ejercicio
↓
se genera un archivo .c
↓
las instrucciones aparecen como comentario inicial bonito
```

El resultado no debe sentirse como una copia cruda del PDF. Debe sentirse como una colección de ejercicios individuales, limpios, navegables y con nombres útiles.

## Decisión nueva

El formato anterior queda descartado.

No se deben usar títulos visibles como:

```text
Sección 1
PDF 2
Página 3
Parte I - Ejercicio 12
Ciclos, condiciones y funciones - 1
Alejandro PDF Ejercicio 001
```

La sección, página y numeración original pueden conservarse como metadata interna, pero no como título principal mostrado al usuario.

## Fuente base

Usar como fuente el PDF:

```text
Problemas de programacion.pdf
```

El documento contiene una parte inicial de evaluación de expresiones y preguntas conceptuales. Esa parte no debe convertirse en ejercicios individuales de programación.

## Qué excluir

Excluir la primera selección de ejercicios de evaluación de expresiones, específicamente el bloque inicial de `Tipos de variables y expresiones`, incluyendo los ejercicios 1 al 21, porque no son problemas de programación completos para crear archivos `.c` individuales dentro de la extensión.

Excluir también:

```text
- encabezados institucionales;
- autores y correos;
- números de página;
- notas generales del folleto;
- enlaces antiguos de interés;
- texto que no sea enunciado de ejercicio;
- numeración usada solo por el PDF;
- separadores visuales del documento;
- títulos de sección usados como título final del ejercicio.
```

## Qué incluir

Incluir los problemas programables a partir del bloque:

```text
Ciclos, condiciones y funciones
```

Y continuar con los bloques programables posteriores:

```text
- Ciclos, condiciones y funciones
- Punteros, arreglos y matrices
- Cadenas de caracteres
- Recursividad
- Estructuras
- Archivos
- Parte II. Otros ejercicios
```

Incluir ejercicios aunque sean largos, siempre que pidan escribir un programa, una función, una simulación, un algoritmo o una solución implementable en C.

## Regla sobre el texto original

No cambiar el sentido del ejercicio.

Se permite:

```text
- limpiar saltos de línea dañados por el PDF;
- corregir errores obvios de OCR;
- unificar espacios;
- preservar fórmulas con mejor formato Markdown;
- separar ejemplos de salida en bloques de código;
- convertir incisos a listas;
- añadir una mini introducción neutral si ayuda a que se vea como Exercism.
```

No se permite:

```text
- resolver el ejercicio;
- añadir requisitos nuevos;
- cambiar datos numéricos;
- cambiar fórmulas;
- eliminar restricciones importantes;
- inventar entradas o salidas obligatorias no presentes en el PDF;
- convertir una pregunta conceptual excluida en ejercicio programable.
```

## Títulos estilo Exercism

Cada ejercicio debe recibir un título humano, corto y descriptivo, derivado del problema real.

Ejemplos esperados:

```text
Imprimir nombre N veces
Conversor de pies y pulgadas a metros
Suma acumulada hasta N
Pulgadas a yardas, pies y pulgadas
Impares menores que 20
Impares descendentes
Máximo de dos valores
Tabla ASCII
Media armónica
Conversión Fahrenheit a Celsius
Divisores exactos
Multiplicación por sumas sucesivas
Ahorro con interés mensual
Conejos de Fibonacci
El inventor del ajedrez
Resumen de diez valores
División por restas sucesivas
Nómina semanal
Solución positiva de una cuadrática
Tipo de triángulo
Aproximación de pi
Proyección de población
Área y circunferencia de un círculo
Potencia por multiplicaciones sucesivas
Volumen de una esfera
Dieta de cuatro semanas
Ternas pitagóricas
Gráficos con asteriscos
Primos del 1 al 100
Conteo de dígitos
Factorización prima
Adivina el número
Cambio mínimo
Problemas de calendario
Persistencia multiplicativa
```

Reglas del título:

```text
- máximo recomendado: 2 a 7 palabras;
- sin prefijos como “Ejercicio 1”;
- sin “PDF”, “sección”, “página” ni “parte”;
- usar español natural;
- usar mayúscula inicial normal, no todo en mayúsculas;
- si el ejercicio tiene nombre narrativo, conservarlo: “El inventor del ajedrez”, “Conejos de Fibonacci”, “La Torre de Hanoi”.
```

## ID y slug

Cada ejercicio debe tener un ID estable basado en el título:

```text
alejandro-<slug-del-titulo>
```

Ejemplos:

```text
alejandro-imprimir-nombre-n-veces
alejandro-conversor-pies-pulgadas-metros
alejandro-conejos-de-fibonacci
alejandro-inventor-del-ajedrez
alejandro-adivina-el-numero
```

Reglas:

```text
- minúsculas;
- sin acentos;
- sin ñ, reemplazar por n;
- palabras separadas por guion;
- no depender de número de página;
- no depender de sección visible del PDF;
- si hay dos títulos parecidos, añadir una palabra diferenciadora, no el número del PDF salvo como último recurso.
```

## Formato de `instructions.md`

Cada Gist tendrá un archivo `instructions.md` con formato limpio.

Plantilla:

```markdown
# Título del ejercicio

## Introducción

Breve contexto de 1 a 3 oraciones, escrito de forma natural, sin resolver el problema.

## Instrucciones

Texto del ejercicio, respetando el enunciado original, pero limpiado y organizado para lectura.

## Ejemplo

```text
Entrada/salida de ejemplo si el PDF la incluye.
```

## Notas

Restricciones o aclaraciones originales del PDF, si existen.
```

Si el PDF no trae ejemplo, omitir la sección `Ejemplo`.

Si el PDF no necesita introducción, usar una introducción mínima de una oración.

## Comentario inicial en el archivo `.c`

Cuando la extensión genere `main.c`, debe insertar las instrucciones como comentario inicial legible.

Ejemplo:

```c
/*
 * Imprimir nombre N veces
 *
 * Escriba un programa al cual se le digite un número e imprima
 * por pantalla su nombre tantas veces como lo indique el número digitado.
 */

#include <stdio.h>

int main(void) {
    return 0;
}
```

La versión Markdown completa vive en el Gist. La versión insertada en el `.c` debe ser una versión adaptada a comentario, legible y sin exceso de ruido.

## Estructura por Gist

Cada ejercicio debe publicarse en un Secret Gist independiente.

Estructura recomendada:

```text
Gist: alejandro-imprimir-nombre-n-veces
├── instructions.md
├── metadata.json
└── icon.svg
```

Opcionalmente:

```text
starter.c
```

si el ejercicio necesita una plantilla inicial distinta a `main.c` básico.

## `metadata.json`

Cada ejercicio debe tener metadata estructurada.

Esquema recomendado:

```json
{
  "id": "alejandro-imprimir-nombre-n-veces",
  "title": "Imprimir nombre N veces",
  "source": "alejandro-pdf",
  "language": "c",
  "filename": "main.c",
  "original": {
    "pdf": "Problemas de programacion.pdf",
    "section": "Ciclos, condiciones y funciones",
    "originalNumber": 1,
    "page": 1
  },
  "filters": {
    "topic": ["entrada-salida", "ciclos"],
    "constructs": ["scanf", "printf", "for"],
    "difficulty": "introductorio",
    "kind": "programa",
    "requiresFunction": false,
    "requiresArrays": false,
    "requiresStrings": false,
    "requiresStructs": false,
    "requiresFiles": false,
    "requiresRecursion": false,
    "requiresRandom": false,
    "inputStyle": "teclado",
    "outputStyle": "consola"
  },
  "gist": {
    "visibility": "secret",
    "files": ["instructions.md", "metadata.json", "icon.svg"]
  }
}
```

La metadata puede conservar sección, página y número original, pero solo para trazabilidad interna. La extensión no debe mostrar esos datos como título principal.

## Filtros para la extensión

El proceso de curación debe generar filtros para que la extensión pueda organizar los ejercicios.

Filtros mínimos:

```text
- fuente: Alejandro
- lenguaje: C
- tema principal
- dificultad
- tipo: programa / función / simulación / juego / conversión / matemática / cadenas / archivos
- requiere ciclos
- requiere condiciones
- requiere funciones
- requiere arreglos
- requiere matrices
- requiere punteros
- requiere cadenas
- requiere recursividad
- requiere estructuras
- requiere archivos
- requiere números aleatorios
- entrada por teclado
- salida por consola
- redireccionamiento
```

Dificultades recomendadas:

```text
introductorio
basico
intermedio
avanzado
reto
```

La dificultad debe inferirse por conceptos requeridos, no por el número original del PDF.

## Iconos SVG

Cada ejercicio debe tener un `icon.svg` simple y descriptivo, en una línea parecida a los iconos simples de Exercism.

Reglas:

```text
- SVG propio, no copiado de Exercism ni de librerías externas;
- vista recomendada: 64x64;
- estilo lineal o geométrico simple;
- uno o dos elementos visuales máximo;
- sin texto dentro del icono;
- sin dependencias externas;
- color neutro o currentColor;
- que pueda verse bien pequeño en la UI.
```

Ejemplos de conceptos:

```text
Imprimir nombre N veces       → líneas repetidas / tarjeta de texto
Conversor de unidades         → regla / flechas de conversión
Fibonacci                     → espiral simple / pares de conejos abstractos
Ajedrez                       → tablero / grano duplicándose
Primos                        → puntos separados / tamiz
Calendario                    → calendario minimalista
Adivina el número             → signo de pregunta / diana
Cadenas                       → comillas / cadena de eslabones
Archivos                      → hoja de documento
Recursividad                  → flecha circular / función llamándose
Matrices                      → cuadrícula
```

## Salida esperada del proceso de curación

El agente debe producir una carpeta local previa a publicación:

```text
.generated/alejandro-exercises/
├── alejandro-imprimir-nombre-n-veces/
│   ├── instructions.md
│   ├── metadata.json
│   └── icon.svg
├── alejandro-conversor-pies-pulgadas-metros/
│   ├── instructions.md
│   ├── metadata.json
│   └── icon.svg
└── catalog.private.json
```

`catalog.private.json` debe contener la lista de ejercicios ya curados, pero todavía sin Gist IDs si no han sido publicados.

Después del proceso de publicación, el catálogo privado puede enriquecerse con:

```json
{
  "id": "alejandro-imprimir-nombre-n-veces",
  "title": "Imprimir nombre N veces",
  "gistId": "GIST_ID_REAL",
  "rawFile": "instructions.md",
  "iconFile": "icon.svg",
  "revision": "2026-05-12-01"
}
```

Este catálogo privado no debe subirse al repo público.

## Publicación a GitHub Gist

Después de curar los ejercicios:

```text
1. Crear un Secret Gist por ejercicio.
2. Subir instructions.md, metadata.json e icon.svg.
3. Guardar el gistId real en el catálogo privado.
4. Inyectar el catálogo privado en el VSIX durante build.
5. Verificar que la extensión puede descargar un ejercicio real.
```

## Migración / borrado del formato anterior

Eliminar cualquier formato anterior basado en:

```text
- secciones visibles del PDF;
- nombres tipo PDF/page/section;
- un único Gist con muchos ejercicios;
- manifest público con Gist IDs reales;
- ejercicios mostrados con numeración cruda del PDF;
- archivos generados con instrucciones sin curar.
```

Si ya existen datos antiguos, crear una migración:

```text
old-id → new-id
```

Ejemplo:

```json
{
  "parte-i-ciclos-1": "alejandro-imprimir-nombre-n-veces",
  "pdf-page-1-exercise-2": "alejandro-conversor-pies-pulgadas-metros"
}
```

La extensión puede usar esa tabla solo para compatibilidad temporal. El catálogo final debe usar únicamente los IDs nuevos.

## Checklist de validación

Antes de publicar cada ejercicio, verificar:

```text
[ ] El título no contiene “PDF”, “sección”, “página” ni numeración cruda.
[ ] El ejercicio viene de un problema programable, no de evaluación de expresiones.
[ ] El enunciado conserva el sentido original.
[ ] Las instrucciones están limpias y legibles.
[ ] Existe metadata.json válido.
[ ] Existen filtros útiles.
[ ] Existe icon.svg simple y válido.
[ ] El ID es estable y legible.
[ ] El Gist contiene un solo ejercicio.
[ ] El ejercicio puede descargarse desde la extensión.
[ ] El archivo .c generado contiene comentario inicial bonito.
```

## Prompt directo para el agente

```text
Eres un asistente de curación de ejercicios para la extensión Estudio Socrático.

Tu tarea es convertir el PDF `Problemas de programacion.pdf` en ejercicios individuales estilo Exercism para estudiantes de C.

Reglas obligatorias:

1. Excluye el bloque inicial de `Tipos de variables y expresiones`, incluyendo los ejercicios 1 al 21. Esos ejercicios son de evaluación/sustitución de expresiones y no deben convertirse en ejercicios de la extensión.
2. Incluye solo problemas programables: programas, funciones, simulaciones, algoritmos, cadenas, arreglos, recursividad, estructuras, archivos y juegos.
3. No uses como título visible nombres como “Sección 1”, “PDF 2”, “Página 3”, “Parte I” ni numeración cruda.
4. Genera para cada problema un título corto y natural en español, parecido al estilo de Exercism.
5. Conserva el sentido del enunciado original. Puedes limpiar formato, saltos de línea, listas y ejemplos, pero no cambies requisitos ni resuelvas el ejercicio.
6. Genera `instructions.md` con introducción breve, instrucciones limpias, ejemplos si existen y notas si existen.
7. Genera `metadata.json` con ID, título, fuente, sección original, número original, filtros y nombre de archivo.
8. Genera `icon.svg` simple, propio y descriptivo para cada ejercicio.
9. Cada ejercicio debe estar preparado para publicarse como un Secret Gist independiente.
10. El resultado final debe servir para que la extensión muestre el título, permita filtrar ejercicios y cree un archivo `.c` con las instrucciones como comentario inicial bonito.
11. El formato anterior basado en secciones del PDF, páginas o numeración cruda queda eliminado.

Entrega la salida como carpetas por ejercicio, cada una con `instructions.md`, `metadata.json` e `icon.svg`, más un `catalog.private.json` de uso interno para empaquetar el VSIX.
```
