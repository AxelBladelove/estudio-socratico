# Manejo de la API key de Gemini en Estudio Socrático

## Decisión final

Para la versión 1.2, la API key de Gemini se manejará como una **clave compartida de conveniencia**, configurada automáticamente por el instalador.

El usuario no tendrá que:

- crear una API key propia;
- entrar a Google AI Studio;
- editar archivos manualmente;
- crear variables de entorno manualmente;
- copiar secretos desde un README;
- configurar Gemini desde la extensión.

El instalador se encargará de descargar o reconstruir la configuración necesaria y dejarla lista localmente.

## Criterio de seguridad aceptado

La clave no se considera secreta en sentido estricto dentro de esta arquitectura.

El objetivo no es impedir completamente que alguien técnico la extraiga. El objetivo es:

- evitar que GitHub bloquee pushes por secret scanning;
- evitar que la clave aparezca literal en el repositorio público;
- evitar configuración manual para el usuario;
- permitir que la herramienta funcione inmediatamente después de instalarse.

Esta decisión se acepta porque el proyecto prioriza simplicidad, costo cero y baja fricción para un grupo reducido de usuarios.

## Dónde no debe vivir la API key

La API key real no debe vivir directamente en el repo público.

No deben existir archivos públicos como:

```text
config.json
.env
.env.local
gemini-key.json
runtime-config.json
soporte/exercism/config.json con clave real
```

Tampoco debe aparecer hardcodeada de forma literal en scripts públicos.

## Dónde sí puede vivir la configuración

La configuración real puede vivir en una fuente no listada o generada durante el empaquetado.

Opciones aceptadas para 1.2:

```text
1. Secret Gist de runtime config.
2. Archivo generado dentro del VSIX final.
3. Archivo privado local usado durante el empaquetado.
```

La opción preferida es combinar estas dos piezas:

```text
VSIX final
→ contiene el identificador o bootstrap necesario para encontrar la runtime config

Secret Gist de runtime config
→ contiene la clave dividida, codificada u ofuscada
```

## Runtime config

El instalador descargará una runtime config desde una ubicación no listada.

Ejemplo conceptual:

```json
{
  "gemini": {
    "mode": "shared",
    "model": "gemini-2.5-flash",
    "keyEncoding": "parts",
    "keyParts": [
      "AIza",
      "Sy...",
      "...",
      "..."
    ]
  }
}
```

El uso de `keyParts`, Base64 u otro formato similar no se considera seguridad fuerte. Su función principal es evitar que la clave aparezca como string literal detectable en el repositorio público.

## Flujo del instalador

El instalador debe ejecutar este flujo:

```text
1. Crear carpeta local de configuración.
2. Obtener bootstrap de runtime config desde el VSIX o desde el instalador empaquetado.
3. Descargar runtime config desde Secret Gist o fuente no listada.
4. Reconstruir la API key de Gemini.
5. Guardar la configuración localmente.
6. Validar que la extensión pueda leerla.
7. Continuar con la instalación normal.
```

Ruta recomendada para configuración local:

```text
%APPDATA%\EstudioSocratico\config.json
```

Ejemplo de config local final:

```json
{
  "gemini": {
    "mode": "shared",
    "apiKey": "AIza...",
    "model": "gemini-2.5-flash"
  },
  "content": {
    "provider": "gist",
    "catalogSource": "bundled-vsix"
  }
}
```

## Relación con VS Code SecretStorage

La extensión puede leer primero la configuración local en `%APPDATA%\EstudioSocratico\config.json`.

Opcionalmente, en su primera activación, puede migrar la API key a `ExtensionContext.secrets` de VS Code.

Flujo opcional:

```text
Extensión se activa
↓
lee config local
↓
si encuentra Gemini key
↓
guarda la key en SecretStorage
↓
usa SecretStorage en ejecuciones futuras
```

Esto mejora el manejo local sin pedir nada al usuario.

## Prioridad de lectura de configuración

La extensión o los scripts deben buscar Gemini en este orden:

```text
1. VS Code SecretStorage, si ya fue migrada.
2. %APPDATA%\EstudioSocratico\config.json.
3. Variable de entorno GEMINI_API_KEY.
4. Archivo legacy soporte/exercism/config.json, solo por compatibilidad.
5. Si no existe nada, mostrar error guiado desde el instalador o la TUI.
```

La variable de entorno `GEMINI_API_KEY` puede mantenerse como fallback para usuarios avanzados, pero no debe ser el flujo principal para usuarios normales.

## Instalación sin interacción manual

El usuario no debe tener que configurar Gemini.

El flujo esperado es:

```text
Usuario ejecuta instalador
↓
Instalador descarga runtime config
↓
Instalador reconstruye la key
↓
Instalador guarda config local
↓
Extensión queda lista
↓
Usuario hace clic en ejercicio
↓
Gemini funciona si se necesita traducir o insertar instrucciones
```

## Uso de Gemini en el flujo de ejercicios

Gemini se usará para las tareas que ya existen o que se mantengan en el sistema, por ejemplo:

```text
- traducir instrucciones de Exercism;
- normalizar instrucciones de ejercicios externos;
- generar o adaptar el primer comentario del archivo;
- mantener compatibilidad con el flujo existente del proyecto.
```

Si un ejercicio de Alejandro ya viene con instrucciones listas en español, Gemini no debería usarse innecesariamente.

## Fallback si falla Gemini

Si Gemini falla, el ejercicio no debe dejar de descargarse.

La extensión debe poder crear el ejercicio con las instrucciones originales.

Flujo de fallback:

```text
Descargar ejercicio
↓
Intentar Gemini si hace falta
↓
Si Gemini falla
↓
Insertar instrucciones originales
↓
Mostrar advertencia no bloqueante
```

El fallo de Gemini no debe bloquear el inicio del ejercicio.

## Archivos generados y .gitignore

Deben ignorarse archivos como:

```text
runtime-config.private.json
private-gemini.generated.ts
gemini-key.local.json
app-config.generated.json
```

Ejemplo de `.gitignore`:

```gitignore
# Estudio Socrático private runtime
runtime-config.private.json
private-gemini.generated.ts
gemini-key.local.json
app-config.generated.json
```

## Implicación importante

Aunque la key no viva en el repo público, puede quedar accesible para alguien que inspeccione el VSIX, la configuración local o el tráfico de la aplicación.

Esto es aceptado para 1.2.

La arquitectura no pretende ser una solución empresarial de secretos. Pretende ser una solución simple, gratis y funcional para un proyecto pequeño.

## Resumen operativo

La funcionalidad final queda así:

```text
API key real
↓
fuente no listada o config generada
↓
instalador la descarga/reconstruye
↓
config local en %APPDATA%\EstudioSocratico
↓
extensión la lee
↓
Gemini funciona sin intervención del usuario
```

## Decisión cerrada

Para la versión 1.2:

```text
✔ El usuario no configura Gemini.
✔ La key no vive literal en el repo público.
✔ El instalador descarga o reconstruye la config.
✔ La config queda guardada localmente.
✔ SecretStorage puede usarse después como mejora.
✔ Gemini no debe bloquear la creación del ejercicio si falla.
✔ Se acepta que la key sea extraíble por alguien técnico.
```
