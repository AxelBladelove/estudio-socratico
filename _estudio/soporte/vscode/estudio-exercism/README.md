# Estudio Socratico - Extension VS Code

Esta extension muestra el panel de ejercicios de Estudio Socratico, registra el
atajo F9 para compilar el archivo C activo y puede usar una API Key propia del
estudiante para funciones opcionales como traducciones o importacion avanzada.

## F9

La extension contribuye el comando:

```text
Estudio Socratico: Compilar archivo C activo
```

con el atajo:

```text
F9
```

El comando ejecuta la tarea del workspace `Compilar y Grabar (Sistema
Socratico)`. Si esa tarea no existe, llama directamente a:

```text
_estudio/soporte/scripts/build.cmd <archivo.c>
```

`Ctrl+Shift+B` sigue funcionando porque usa la misma tarea de build por defecto
del workspace.

## API Key BYOK

La API Key no se pide dentro del instalador. Para configurarla, abre el panel de la extension y pulsa **Abrir configuracion de API Key**. Esto crea y abre:

```text
usuario/config/estudio-socratico.extension.local.json
```

Ese archivo es local, esta ignorado por Git y no debe compartirse. La extension lo usa como fuente principal para BYOK; `GEMINI_API_KEY` queda solo como fallback opcional. Puedes cambiar `provider`, `apiKey`, `model` y `features` manualmente:

```json
{
  "provider": "gemini",
  "apiKey": "TU_API_KEY_AQUI",
  "model": "gemini-2.5-flash",
  "features": {
    "translateIntroductions": true,
    "importExercism": true,
    "importAlejandroGists": true
  }
}
```

El ejemplo versionado vive en:

```text
usuario/config/estudio-socratico.extension.example.json
```

La extension sigue funcionando parcialmente sin API Key. Si falta la key, solo las funciones que dependan del proveedor BYOK deben avisar; esto no es el token de Exercism CLI.
