namespace Estudio.Setup.Services;

public static class SetupPackageLayout
{
    public const string PayloadDirectoryName = "payload";
    public const string ManifestFileName = "manifest.json";
    public const string ChecksumsFileName = "checksums.sha256";
    public const string FrameworkArchiveFileName = "estudio-framework.zip";
    public const string InstallerExecutableFileName = "Instalar Estudio Socrático.exe";
    public const string ReadmeFileName = "README.txt";

    public static string ResolvePayloadRoot(string setupRoot)
    {
        var root = Path.GetFullPath(setupRoot);
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        if (string.Equals(name, PayloadDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return Path.Combine(root, PayloadDirectoryName);
    }

    public static bool LooksLikePackagedInstaller(string setupRoot)
    {
        var payloadRoot = ResolvePayloadRoot(setupRoot);
        return Directory.Exists(payloadRoot)
            || File.Exists(Path.Combine(payloadRoot, ManifestFileName));
    }
}