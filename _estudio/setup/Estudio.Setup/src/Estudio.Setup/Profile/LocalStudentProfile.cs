namespace Estudio.Setup.Profile;

public static class LocalStudentProfile
{
    private const string IdentityFileName = ".estudio_usuario";
    private const string AliasEnvironmentVariable = "ESTUDIO_USUARIO";
    private const string FallbackAlias = "estudiante";
    private const string AliasPattern = "^[A-Za-z0-9_](?:[A-Za-z0-9_-]*[A-Za-z0-9_])?$";

    public static string ResolveAlias(string workspaceRoot)
    {
        var path = Path.Combine(workspaceRoot, IdentityFileName);
        if (File.Exists(path))
        {
            var alias = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(alias))
            {
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
        }

        foreach (var candidate in EnumerateFallbackAliases(workspaceRoot))
        {
            if (TryResolveValidAlias(candidate, out var alias))
            {
                return alias;
            }
        }

        return FallbackAlias;
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
        DirectoryInfo? fallbackWorkspace = null;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, IdentityFileName)))
            {
                return current.FullName;
            }

            if (fallbackWorkspace is null && LooksLikeWorkspaceRoot(current.FullName))
            {
                fallbackWorkspace = current;
            }

            current = current.Parent;
        }

        return fallbackWorkspace?.FullName ?? Path.GetFullPath(startDirectory);
    }

    private static IEnumerable<string?> EnumerateFallbackAliases(string workspaceRoot)
    {
        yield return Environment.GetEnvironmentVariable(AliasEnvironmentVariable);
        yield return ReadGitConfigValue(workspaceRoot, "github", "user");
        yield return ReadGitConfigValue(workspaceRoot, "user", "name");
        yield return Environment.UserName;
    }

    private static bool TryResolveValidAlias(string? candidate, out string alias)
    {
        alias = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();
        try
        {
            ValidateAlias(trimmed);
            alias = trimmed;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string? ReadGitConfigValue(string workspaceRoot, string sectionName, string keyName)
    {
        var configPath = Path.Combine(workspaceRoot, ".git", "config");
        if (!File.Exists(configPath))
        {
            return null;
        }

        string? currentSection = null;
        foreach (var rawLine in File.ReadLines(configPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            if (!string.Equals(currentSection, sectionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var currentKey = line[..separatorIndex].Trim();
            if (string.Equals(currentKey, keyName, StringComparison.OrdinalIgnoreCase))
            {
                return line[(separatorIndex + 1)..].Trim();
            }
        }

        return null;
    }

    private static bool LooksLikeWorkspaceRoot(string directory)
    {
        return Directory.Exists(Path.Combine(directory, ".git"))
            || File.Exists(Path.Combine(directory, "_estudio", "setup", "Estudio.Setup.cmd"))
            || (File.Exists(Path.Combine(directory, "Estudio.Setup.cmd"))
                && Directory.Exists(Path.Combine(directory, "_estudio", "setup")));
    }
}
