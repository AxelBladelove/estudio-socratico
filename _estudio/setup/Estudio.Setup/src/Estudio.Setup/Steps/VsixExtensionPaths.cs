namespace Estudio.Setup.Steps;

public static class VsixExtensionPaths
{
    public const string ExtensionId = "estudio-socratico.estudio-exercism";
    public const string PackageFileName = "estudio-exercism-2.0.0.vsix";

    public static string ResolveVsixPath(string workspaceRoot)
    {
        var candidates = new[]
        {
            Path.Combine(workspaceRoot, "_estudio", "soporte", "runtime", "vscode", "estudio-exercism.vsix"),
            Path.Combine(workspaceRoot, "extension", "estudio-socratico-2.0.0.vsix"),
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    public static string ResolveExtensionSourceDirectory(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, "_estudio", "soporte", "vscode", "estudio-exercism");
    }

    public static string ResolvePackagedVsixPath(string workspaceRoot)
    {
        return Path.Combine(ResolveExtensionSourceDirectory(workspaceRoot), PackageFileName);
    }
}
