# Prompt Para Cifrar La API Key De Gemini En La PC De Erick

## Importante

- Este flujo usa `soporte/exercism/proteger_gemini.ps1`, que ya existe en el repo.
- El resultado queda en `soporte/exercism/config.json` como `apiKeyProtected` usando DPAPI de Windows.
- Ese blob cifrado solo funciona en la cuenta de Windows de Erick.
- Si Erick reemplaza y commitea `soporte/exercism/config.json`, podra hacer commit y push sin exponer la key en claro, pero ese blob no va a servir en otras PCs o usuarios de Windows.
- Si despues alguien mas necesita usar Gemini localmente, tendra que volver a proteger su propia key en su propia maquina.

## Prompt

Pega esto en la IA de Erick:

```text
Estoy en Windows dentro del repo estudio-socratico y necesito cifrar mi Gemini API key para que no quede en claro en Git ni me bloquee los commits por push protection.

Hazlo usando el flujo real que ya existe en este repo. No inventes otro metodo de cifrado ni otra sintaxis.

Requisitos:
1. Lee primero `soporte/exercism/config.json` y `soporte/exercism/proteger_gemini.ps1` para confirmar el formato actual.
2. Usa el script `./soporte/exercism/proteger_gemini.ps1` para proteger mi key con DPAPI de Windows en mi propia cuenta de usuario.
3. Si mi key no esta disponible todavia, pidemela una sola vez o indicame que la ponga temporalmente en `GEMINI_API_KEY` en esta sesion.
4. No imprimas mi API key en la respuesta, en logs, ni en archivos.
5. No toques archivos no relacionados.
6. No hagas commits ni push a menos que yo lo pida despues.

Quiero que ejecutes este flujo:
1. Verifica el contenido actual de `soporte/exercism/config.json`.
2. Si hace falta, preguntame por la key o usa `GEMINI_API_KEY` si ya existe en la sesion.
3. Ejecuta `./soporte/exercism/proteger_gemini.ps1` para regenerar `soporte/exercism/config.json` con mi key cifrada.
4. Valida que en `soporte/exercism/config.json` ya no exista ninguna propiedad en claro tipo `apiKey`, `geminiApiKey` o `GEMINI_API_KEY`.
5. Valida que solo quede el modelo y `apiKeyProtected`.
6. Ejecuta una comprobacion final para confirmar que `./soporte/exercism/manager.ps1 -Action status` devuelve `geminiConfigured=true`.
7. Ensename un resumen corto de lo que cambiaste y confirmame expresamente si ya puedo hacer commit y push sin subir la key en texto plano.

Contexto importante:
- Este cifrado sirve para esta PC y este usuario de Windows.
- Si luego este `config.json` se comparte con otra persona, ese blob no se podra descifrar alla.
- Aun asi, GitHub no deberia detectarlo como secreto en claro, que es justo lo que necesito para poder commitear y pushear desde esta maquina.
```