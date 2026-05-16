namespace Estudio.Setup.Core;

public static class SetupHelp
{
    public const string Text = """
        Estudio.Setup 2.0

        Uso:
          Estudio.Setup.exe [install|update|repair|verify|package] [opciones]

        Modos:
          install   Instala o completa componentes faltantes.
          update    Actualiza componentes controlados por Estudio Socratico.
          repair    Repara componentes faltantes o invalidos.
          verify    Diagnostica sin instalar ni cambiar el sistema.
          package   Genera el instalador self-contained, manifest y ZIP.

        Opciones:
          --alias <valor>       Usa un alias temporal para calcular fork/settings.
          --change-github       Fuerza logout/login de GitHub CLI antes de reparar fork/remotes.
          --only <step-id>      Ejecuta solo un componente. Puede repetirse.
          --state-root <ruta>   Guarda setup-state, logs y reporte en otra carpeta.
          --tui                 Abre la interfaz Terminal.Gui de progreso/reintentos.
          --help                Muestra esta ayuda.
        """;
}
