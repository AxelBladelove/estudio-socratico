namespace Estudio.Setup.State;

public static class SetupPathDefaults
{
    public static string ResolveStateRoot(string? explicitPath)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return ResolveStateRoot(explicitPath, localAppData);
    }

    public static string ResolveStateRoot(string? explicitPath, string localAppData)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("LOCALAPPDATA no esta disponible para guardar estado del instalador.");
        }

        return Path.Combine(localAppData, "EstudioSocratico");
    }

    public static string ResolveLogRoot(string stateRoot)
    {
        return Path.Combine(stateRoot, "logs");
    }
}
