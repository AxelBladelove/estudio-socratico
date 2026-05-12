# Publicación de Ejercicios a GitHub Gists

Este documento describe el flujo automatizado para publicar y actualizar los ejercicios extraídos del PDF en GitHub Secret Gists, manteniendo un catálogo dinámico consumible por la extensión.

## 1. Validación de ejercicios

Tras la extracción y curación (via `curator.py`), todos los ejercicios quedan generados localmente en `.generated/alejandro-exercises/`.
Asegúrate de revisar la carpeta para validar que los iconos, títulos y filtros sean correctos. Existe un reporte en `.generated/alejandro-exercises-validation-report.md`.

## 2. Publicación de Secret Gists

Para publicar los ejercicios como Gists, se utiliza un script en Node.js que requiere autenticación de GitHub del desarrollador (ya sea mediante el CLI `gh` o la variable `GITHUB_TOKEN`). 

Ejecuta:
```bash
npm run alejandro:gists:publish
```
*Si falla indicando problemas de autenticación, ejecuta `gh auth login` primero.*

El script es **idempotente**. Se mantendrá un archivo de estado local para evitar duplicar ejercicios, y solo hará `PATCH` a los gists existentes si corres el comando de nuevo.

## 3. Catálogo y Extension Inyectable

Tras correr la publicación, se genera automáticamente `.generated/gists/alejandro-private-manifest.json`.
Este archivo cuenta con los Gist IDs reales y las raw URLs para la descarga. 

Para inyectarlo en el código fuente de la extensión antes de empaquetar el VSIX, ejecuta:
```bash
npm run alejandro:gists:manifest
```
Este script leerá el manifest privado y creará el archivo `src/generated/alejandro-catalog.ts`. 

## 4. Archivos Ignorados

Asegúrate de que el `.gitignore` tiene las siguientes exclusiones:
```text
.generated/gists/
*.private-manifest.json
*gist-state*.json
src/generated/
```

## 5. Privacidad Práctica

> **Nota de Seguridad Práctica**: Los Secret Gists funcionan como contenido no listado. No aparecen públicamente en búsquedas normales, pero cualquier persona con la URL puede acceder al contenido. Este modelo se acepta por simplicidad, costo cero y baja fricción. El archivo `alejandro-catalog.ts` no debe commitearse en el repositorio para que los Gist IDs no queden expuestos directamente en el código público, aunque una vez distribuida la extensión, es posible leerlos desensamblando el VSIX.

## 6. Mantenimiento del Estado Local

Si en algún momento el archivo de estado pierde los hashes calculados (por ejemplo, después de una migración del formato de estado) y el publicador intenta hacer `PATCH` de todos los archivos innecesariamente golpeando el Rate Limit de GitHub, puedes utilizar el script de reparación:

```bash
node soporte/gists/repair-gist-state-hashes.mjs
```

Este script es **transitorio pero permanente**. Lee el estado local existente, calcula el hash SHA-256 exacto del contenido en local, e inyecta el hash dentro del JSON sin realizar ninguna petición a la API de GitHub. De esta forma la idempotencia se restaura y el publisher omitirá correctamente los archivos que no han cambiado.
