using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class VsixExtensionPathsTests
{
    [Fact]
    public void ResolveVsixPath_prefers_new_estudio_runtime_path()
    {
        var root = MakeTempRoot();
        var expected = Path.Combine(root, "_estudio", "soporte", "runtime", "vscode", "estudio-exercism.vsix");
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.WriteAllText(expected, "vsix");

        var resolved = VsixExtensionPaths.ResolveVsixPath(root);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void ResolveVsixPath_ignores_legacy_untracked_soporte_path()
    {
        var root = MakeTempRoot();
        var legacy = Path.Combine(root, "soporte", "runtime", "vscode", "estudio-exercism.vsix");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "legacy");

        var resolved = VsixExtensionPaths.ResolveVsixPath(root);

        Assert.Equal(
            Path.Combine(root, "_estudio", "soporte", "runtime", "vscode", "estudio-exercism.vsix"),
            resolved);
    }

    [Fact]
    public void ResolveExtensionSourceDirectory_returns_tracked_extension_project()
    {
        var root = MakeTempRoot();

        var resolved = VsixExtensionPaths.ResolveExtensionSourceDirectory(root);

        Assert.Equal(Path.Combine(root, "_estudio", "soporte", "vscode", "estudio-exercism"), resolved);
    }

    [Fact]
    public void ResolvePackagedVsixPath_returns_current_package_name_inside_extension_project()
    {
        var root = MakeTempRoot();

        var resolved = VsixExtensionPaths.ResolvePackagedVsixPath(root);

        Assert.Equal(
            Path.Combine(root, "_estudio", "soporte", "vscode", "estudio-exercism", "estudio-exercism-2.0.0.vsix"),
            resolved);
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }
}
