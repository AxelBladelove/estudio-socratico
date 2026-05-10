# Drive Sync Para Mantenedores

Estos scripts son solo para publicar ejercicios en Google Drive. Los estudiantes
no usan OAuth de Drive: descargan enunciados publicos por `driveFileId`.

## Preparar OAuth Local

1. En Google Cloud, habilita Google Drive API.
2. Crea un OAuth Client tipo Desktop app.
3. Descarga el JSON del cliente.
4. Guardalo como:

```text
.estudio-drive/oauth-client.json
```

Ese archivo esta ignorado por Git.

## Comandos

```bat
npm run drive:auth
npm run drive:check
npm run drive:generate
npm run drive:sync
```

Para sincronizar solo W3Schools:

```bat
npm run drive:sync -- --provider w3schools
```

`drive:sync` sube o actualiza archivos Markdown, los deja publicos como solo
lectura y escribe `driveFileId` en el catalogo. Cuando un ejercicio ya esta
subido, elimina `instructionMarkdown` del catalogo versionado para que el repo
de estudiantes no cargue enunciados completos.

## Fuentes Locales

Si un ejercicio no tiene `instructionMarkdown` en el catalogo, coloca su fuente
en:

```text
.estudio-drive/source/<provider>/<slug>.md
```

Ejemplo:

```text
.estudio-drive/source/alejandro/seccion-1-1-imprimir-nombre-n-veces.md
```
