namespace Estudio.Setup.Profile;

public static class LocalStudentProfile
{
    private const string IdentityFileName = ".estudio_usuario";
    private const string FallbackAlias = "estudiante";
    private const string AliasPattern = "^[A-Za-z0-9_](?:[A-Za-z0-9_-]*[A-Za-z0-9_])?$";

    public static string ResolveAlias(string workspaceRoot)
    {
        var path = Path.Combine(workspaceRoot, IdentityFileName);
        if (!File.Exists(path))
        {
            return FallbackAlias;
        }

        var alias = File.ReadAllText(path).Trim();
        if (string.IsNullOrWhiteSpace(alias))
        {
            return FallbackAlias;
        }

        try
        {
            ValidateAlias(alias);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"{IdentityFileName} contiene un alias invalido: {ex.Message}", ex);
        }

        return alias;
    }

    public static void ValidateAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("El alias no puede estar vacio.", nameof(alias));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(alias, AliasPattern))
        {
            throw new ArgumentException(
                "El alias solo puede usar letras, numeros, guion y underscore; no puede tener espacios ni empezar o terminar con guion.",
                nameof(alias));
        }
    }

    public static string FindWorkspaceRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, IdentityFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(startDirectory);
    }
}
