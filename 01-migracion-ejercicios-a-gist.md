# Migración de ejercicios a GitHub Secret Gists

## Decisión final

Para la versión 1.2, los ejercicios externos del proyecto se distribuirán mediante **GitHub Secret Gists**, usando el siguiente modelo:

```text
1 ejercicio = 1 Secret Gist
```

No se usará un único Gist grande con todos los ejercicios. Cada ejercicio tendrá su propio Gist independiente, con su propio identificador y su propio archivo Markdown de instrucciones.

Esta decisión prioriza:

- instalación simple;
- costo cero;
- ausencia de backend;
- ausencia de autenticación de GitHub para el usuario;
- flujo parecido a Exercism: el usuario hace clic y el ejercicio se descarga;
- facilidad para actualizar, reemplazar o retirar ejercicios individuales.

## Nivel real de privacidad

Los Secret Gists no deben tratarse como contenido privado en sentido estricto. En esta arquitectura se aceptan como **contenido no listado**.

Eso significa:

- no aparecen en búsquedas públicas normales;
- no requieren login del usuario;
- cualquier persona con la URL puede acceder al contenido;
- si alguien extrae el identificador del Gist desde la extensión instalada, puede descargar el ejercicio.

Este comportamiento es aceptado para la versión 1.2 porque el objetivo principal es reducir fricción y evitar infraestructura adicional.

## Regla principal de empaquetado

Los identificadores reales de los Gists no deben vivir como archivos visibles en el repositorio público.

No deben existir en el repo público archivos como:

```text
content/manifest.json
private-gists.json
gist-ids.json
runtime.json
```

En su lugar, los identificadores de Gists se inyectarán durante el proceso de empaquetado de la extensión y quedarán dentro del VSIX final.

## Modelo de distribución

El repositorio público contendrá el código de la extensión, scripts, instalador y plantillas.

El VSIX generado contendrá una tabla interna de ejercicios con los identificadores de Gists necesarios para descargar cada ejercicio.

```text
Repositorio público
├── código fuente de la extensión
├── instalador
├── scripts públicos
└── plantillas sin secretos

VSIX final
├── extensión compilada
├── catálogo interno generado
└── identificadores reales de Secret Gists
```

El usuario instalará la extensión mediante el instalador. El usuario no tendrá que configurar GitHub ni autorizar nada.

## Catálogo interno de ejercicios

La extensión debe tener un catálogo interno generado en build time. Este catálogo puede existir como archivo TypeScript generado o como JSON incluido dentro del paquete final.

Ejemplo conceptual:

```ts
export const ALEJANDRO_EXERCISES = [
  {
    id: "alejandro-imprimir-nombre-n-veces",
    title: "Imprimir nombre N veces",
    source: "alejandro",
    language: "c",
    filename: "main.c",
    gistId: "GIST_ID_REAL",
    rawFile: "instructions.md"
  }
];
```

Ese archivo no debe escribirse manualmente en el repositorio público con datos reales. Debe generarse desde una fuente privada local o desde variables de empaquetado.

## Flujo del usuario

El flujo esperado debe ser igual de simple que un ejercicio de Exercism:

```text
Usuario abre VS Code
↓
Panel de Estudio Socrático
↓
Ejercicios de Alejandro
↓
Clic en "Imprimir nombre N veces"
↓
La extensión descarga el Markdown desde su Secret Gist
↓
La extensión crea la carpeta del ejercicio
↓
La extensión crea el archivo base
↓
La extensión inserta las instrucciones como primer comentario
↓
El usuario empieza a programar
```

El usuario no ve:

- GitHub;
- Gists;
- tokens;
- configuración manual;
- URLs;
- manifiestos externos.

## Estructura recomendada de cada Gist

Cada Gist debe representar un solo ejercicio.

Ejemplo:

```text
Gist: alejandro-imprimir-nombre-n-veces
├── instructions.md
└── metadata.json opcional
```

`instructions.md` contendrá el enunciado del ejercicio.

`metadata.json` solo debe usarse si hace falta guardar datos adicionales por ejercicio. En principio, la metadata principal debe vivir en el catálogo interno de la extensión para evitar depender de múltiples lecturas remotas.

## URL raw del ejercicio

La extensión puede construir la URL raw del Gist con el `gistId` y el nombre del archivo.

Ejemplo conceptual:

```text
https://gist.githubusercontent.com/<owner>/<gistId>/raw/instructions.md
```

También puede guardarse directamente la URL raw en el catálogo generado, pero es preferible guardar `gistId` y `rawFile` para mantener el modelo más limpio.

## Cache local

Cada ejercicio descargado debe guardarse en cache local.

Ruta recomendada en Windows:

```text
%APPDATA%\EstudioSocratico\cache\exercises\<exercise-id>\instructions.md
```

La cache permite:

- evitar descargar el mismo ejercicio varias veces;
- funcionar parcialmente si GitHub falla;
- reducir latencia;
- mantener trazabilidad de qué versión del ejercicio fue usada.

## Versionado de ejercicios

Cada entrada del catálogo debe incluir una versión o revisión.

Ejemplo:

```ts
{
  id: "alejandro-imprimir-nombre-n-veces",
  title: "Imprimir nombre N veces",
  gistId: "GIST_ID_REAL",
  rawFile: "instructions.md",
  revision: "2026-05-11-01"
}
```

Si se actualiza el Gist, se incrementa la revisión. La extensión puede comparar la revisión local con la revisión empaquetada y decidir si debe refrescar la cache.

## Proceso de actualización para el desarrollador

El flujo de mantenimiento debe ser automatizado con scripts.

Flujo recomendado:

```text
1. Crear o actualizar Markdown local del ejercicio.
2. Ejecutar script de publicación.
3. El script crea o actualiza un Secret Gist por ejercicio.
4. El script genera un catálogo privado con los gistId.
5. El build de la extensión inyecta ese catálogo en el VSIX.
6. El instalador distribuye el VSIX final.
```

Scripts sugeridos:

```text
soporte/gists/
├── publish-exercise-gist.ps1
├── publish-all-exercises.ps1
├── generate-private-catalog.ps1
└── inject-catalog-into-extension.ps1
```

## Archivos que sí pueden vivir en el repo público

El repo público puede tener plantillas sin datos reales:

```text
extension/src/content/exerciseCatalog.template.ts
soporte/gists/example.private-catalog.json
soporte/gists/README.md
```

Ejemplo de plantilla segura:

```ts
export const ALEJANDRO_EXERCISES = __PRIVATE_EXERCISE_CATALOG__;
```

Durante el empaquetado, `__PRIVATE_EXERCISE_CATALOG__` se reemplaza por el catálogo real.

## Archivos que no deben vivir en el repo público

No deben subirse:

```text
private-catalog.json
runtime-config.json
real-gist-ids.json
real-gemini-key.json
*.generated.private.ts
```

Estos archivos deben estar en `.gitignore`.

## Implicación importante del VSIX

Aunque los Gist IDs no vivan en el repo público, sí estarán dentro del VSIX final.

Eso significa que una persona técnica podría:

```text
1. descargar el VSIX;
2. abrirlo como ZIP;
3. inspeccionar los archivos compilados;
4. encontrar los Gist IDs;
5. descargar ejercicios manualmente.
```

Esto es aceptado para la versión 1.2 porque el objetivo no es seguridad fuerte, sino baja fricción y distribución simple.

## Resumen operativo

La arquitectura final para ejercicios queda así:

```text
Un ejercicio
↓
Un Secret Gist
↓
Gist ID inyectado en el VSIX
↓
Extensión descarga el ejercicio al hacer clic
↓
Cache local
↓
Carpeta y archivo generados automáticamente
```

## Decisión cerrada

Para la versión 1.2:

```text
✔ Un Gist por ejercicio.
✔ Sin backend.
✔ Sin repo privado compartido.
✔ Sin autenticación de GitHub para usuarios.
✔ Gist IDs fuera del repo público.
✔ Gist IDs dentro del VSIX final.
✔ Descarga automática desde la extensión.
✔ Cache local por ejercicio.
```
