using Estudio.Setup.Steps;

namespace Estudio.Setup.Windows;

public static class GuidedSolutionCatalog
{
    public static GuidedSolution? ForBlock(string blockId)
    {
        return blockId switch
        {
            "github-ready" => new GuidedSolution(
                blockId,
                "Conectar GitHub",
                "No pude terminar la conexion con GitHub automaticamente.",
                new[]
                {
                    "Pulsa Abrir GitHub para abrir el inicio de sesion oficial.",
                    "Cuando GitHub confirme tu sesion, vuelve al instalador.",
                    "Yo comprobare la conexion automaticamente y seguire sin pedirte comandos.",
                },
                "Abrir GitHub",
                "https://github.com/login"),
            "workspace-ready" => new GuidedSolution(
                blockId,
                "Preparar tu carpeta de estudio",
                "No pude terminar de preparar tu carpeta de estudio automaticamente.",
                new[]
                {
                    "Comprueba que la carpeta elegida exista y que tengas permisos de escritura.",
                    "Si la carpeta elegida esta dentro del ZIP extraido, cambia la ubicacion a una carpeta normal de tu usuario.",
                    "Vuelve aqui y pulsa Reintentar para que el instalador retome el trabajo.",
                }),
            "vscode-ready" => new GuidedSolution(
                blockId,
                "Dejar VS Code listo",
                "No pude dejar VS Code listo automaticamente.",
                new[]
                {
                    "Cierra cualquier instalacion pendiente de VS Code si la hubiera.",
                    "Pulsa Reintentar para que el instalador revise VS Code otra vez.",
                    "Si vuelve a fallar, abre la pagina oficial y despues vuelve aqui para continuar.",
                },
                "Abrir VS Code",
                "https://code.visualstudio.com/"),
            "extension-ready" => new GuidedSolution(
                blockId,
                "Instalar la extension",
                "No pude instalar la extension de VS Code automaticamente.",
                new[]
                {
                    "Asegurate de que VS Code este instalado y se pueda abrir normalmente.",
                    "Pulsa Reintentar para que el instalador pruebe otra vez la instalacion de la extension.",
                    "Si aun falla, usa la pagina oficial de VS Code y vuelve aqui; el detalle tecnico queda disponible si lo necesitas.",
                },
                "Abrir VS Code",
                "https://code.visualstudio.com/"),
            "compiler-ready" => new GuidedSolution(
                blockId,
                "Preparar herramientas de programacion",
                "No pude preparar las herramientas de programacion automaticamente.",
                new[]
                {
                    "Pulsa Reintentar para volver a probar la instalacion automatica.",
                    "Si vuelve a fallar, abre la ayuda de MSYS2 desde el boton inferior.",
                    "Cuando termines, vuelve aqui. Yo retomare la verificacion sin pedirte terminales ni reinicios manuales si no hacen falta.",
                },
                "Abrir ayuda de MSYS2",
                "https://www.msys2.org/"),
            "exercises-ready" => new GuidedSolution(
                blockId,
                "Preparar tus ejercicios",
                "No pude dejar Exercism y tus ejercicios listos automaticamente.",
                new[]
                {
                    "Si aun no pegaste tu token, abre la pagina oficial y copialo desde Exercism.",
                    "Si ya pegaste tu token pero falta activar los ejercicios de C, se abrira Exercism para hacerlo.",
                    "Cuando termines, vuelve aqui. Yo continuare automaticamente.",
                },
                "Abrir Exercism",
                ExercismCTrackStep.TokenUrl),
            "gemini-ready" => new GuidedSolution(
                blockId,
                "Preparar Gemini local",
                "No pude dejar la configuracion local de Gemini lista automaticamente.",
                new[]
                {
                    "La configuracion privada de Gemini se guarda fuera de tu repo del estudiante.",
                    "Si tu organizacion usa una configuracion bootstrap, vuelve a intentar cuando esa configuracion exista.",
                    "El instalador no copiara claves privadas al ZIP ni a tu fork publico.",
                }),
            "f9-ready" => new GuidedSolution(
                blockId,
                "Activar F9 en VS Code",
                "No pude dejar F9 listo automaticamente.",
                new[]
                {
                    "Abre Estudio Socratico desde el boton final del instalador.",
                    "Cuando VS Code abra la carpeta correcta, vuelve aqui si necesitas reintentar la configuracion.",
                    "El instalador seguira usando tu entorno real, no la carpeta del ZIP.",
                }),
            _ => null,
        };
    }
}