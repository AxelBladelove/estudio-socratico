namespace Estudio.Setup.Steps;

public static class VsCodeSettingsPaths
{
    public static string ResolveSettingsPath(string? appDataRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(appDataRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : appDataRoot;

        return Path.Combine(root, "Code", "User", "settings.json");
    }
}
