namespace Estudio.Setup.Core;

public static class SetupHelp
{
    public const string Text = """
        Estudio.Setup 2.0

        Uso:
          Estudio.Setup.exe [install|update|reinstall|repair|uninstall|verify|package] [opciones]
          Estudio.Setup.exe sin argumentos abre verify --tui y deja elegir el modo desde la UI.

        Modos:
          install   Instala o completa componentes faltantes.
          update    Actualiza componentes controlados por Estudio Socratico.
          reinstall Reaplica configuracion, extension y componentes locales.
          repair    Repara componentes faltantes o invalidos.
          uninstall Limpia integraciones locales de Estudio sin quitar herramientas globales.
          verify    Diagnostica sin instalar ni cambiar el sistema.
          package   Genera el instalador self-contained, manifest y ZIP.

        Opciones:
          --alias <valor>       Usa un alias temporal para calcular fork/settings.
          --change-github       Fuerza logout/login de GitHub CLI antes de reparar fork/remotes.
          --only <step-id>      Ejecuta solo un componente. Puede repetirse.
          --state-root <ruta>   Guarda setup-state, logs y reporte en otra carpeta.
          --tui                 Abre la interfaz visual de progreso/reintentos.
          --events-json         Emite progreso JSON para el frontend Textual.
          --help                Muestra esta ayuda.
        """;
}
