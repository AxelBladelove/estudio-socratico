# Estudio Socrático Configurador UI Drop-in

UI React/Vite para integrar dentro de `_estudio/installer/src/EstudioSocratico.Configurator.App/wwwroot`.

## Qué trae

- Tema oscuro estilo Windows 11 / ChatGPT frontend.
- Flujo:
  1. Inicio
  2. Elegir flujo
  3. Revisión con herramientas individuales
  4. Preparación
  5. Cuentas
  6. Instalación/Limpieza
- Pantalla de revisión con iconos/estados por herramienta.
- Integración inicial con WebView2 bridge en `src/bridgeClient.js`.
- Acciones previstas:
  - `DiagnoseEnvironment`
  - `ConfigureGithub`
  - `ChangeGithubAccount`
  - `ConfigureExercism`
  - `OpenExercismTokenPage`
  - `OpenExercismCTrack` *(debe añadirse al backend)*
  - `ApplyPlan`
  - `ReinstallManaged` *(debe añadirse al backend)*
  - `UninstallManaged` *(debe añadirse al backend)*

## Uso esperado en el repo

Coloca esta carpeta dentro de:

`_estudio/installer/ui`

Luego adapta el script `scripts/build-ui.bat` para ejecutar:

```bat
cd /d _estudio\installer\ui
npm install
npm run build
xcopy /E /I /Y dist ..\src\EstudioSocratico.Configurator.App\wwwroot
```

## Importante

Esta UI trae mock visual para desarrollo, pero el objetivo es conectarla al backend real y reemplazar estados mock con snapshots/eventos del bridge.
