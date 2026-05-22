# Estudio Socratico - Extension VS Code

Esta extension muestra el panel de ejercicios de Estudio Socratico y puede usar una API Key propia del estudiante para funciones opcionales como traducciones o importacion avanzada.

La API Key no se pide dentro del instalador. Para configurarla, abre el panel de la extension y pulsa **Abrir configuracion de API Key**. Esto crea y abre:

```text
usuario/config/estudio-socratico.extension.local.json
```

Ese archivo es local, esta ignorado por Git y no debe compartirse. Puedes cambiar `provider`, `apiKey` y `model` manualmente:

```json
{
  "provider": "gemini",
  "apiKey": "TU_API_KEY_AQUI",
  "model": "gemini-2.5-flash"
}
```

El ejemplo versionado vive en:

```text
usuario/config/estudio-socratico.extension.example.json
```

La extension sigue funcionando parcialmente sin API Key. Si falta la key, solo las funciones que dependan del proveedor BYOK deben avisar; esto no es el token de Exercism CLI.
