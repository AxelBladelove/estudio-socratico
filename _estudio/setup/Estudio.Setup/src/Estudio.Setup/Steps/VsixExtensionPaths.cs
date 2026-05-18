using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public static class VsixExtensionPaths
{
    public const string ExtensionId = "estudio-socratico.estudio-exercism";
    public const string PackageFileName = "estudio-exercism-2.0.0.vsix";
    public const string ReleasePackageFileName = "estudio-socratico-2.0.0.vsix";

    public static string ResolveVsixPath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, "_estudio", "soporte", "runtime", "vscode", "estudio-exercism.vsix");
    }

    public static string ResolveBundledVsixPath(string setupRoot)
    {
        var payloadRoot = SetupPackageLayout.ResolvePayloadRoot(setupRoot);
        var candidates = new[]
        {
            Path.Combine(payloadRoot, ReleasePackageFileName),
            Path.Combine(payloadRoot, "extension", ReleasePackageFileName),
            Path.Combine(setupRoot, "extension", ReleasePackageFileName),
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[^1];
    }

    public static string ResolveInstallableVsixPath(string setupRoot, string workspaceRoot)
    {
        var bundled = ResolveBundledVsixPath(setupRoot);
        return File.Exists(bundled) ? bundled : ResolveVsixPath(workspaceRoot);
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
