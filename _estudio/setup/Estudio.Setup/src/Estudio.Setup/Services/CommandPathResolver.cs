namespace Estudio.Setup.Services;

public static class CommandPathResolver
{
    public static string Resolve(string fileName, string? pathValue, string? pathextValue)
    {
        if (Path.IsPathFullyQualified(fileName) || HasDirectorySegment(fileName))
        {
            return fileName;
        }

        var extensions = BuildExtensions(pathextValue);
        foreach (var directory in SplitPath(pathValue))
        {
            var exact = Path.Combine(directory, fileName);
            if (File.Exists(exact))
            {
                return exact;
            }

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, fileName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return fileName;
    }

    public static bool IsWindowsCommandScript(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDirectorySegment(string fileName)
    {
        return fileName.Contains(Path.DirectorySeparatorChar)
            || fileName.Contains(Path.AltDirectorySeparatorChar);
    }

    private static IEnumerable<string> SplitPath(string? pathValue)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            yield break;
        }

        foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return entry.Trim();
        }
    }

    private static IReadOnlyList<string> BuildExtensions(string? pathextValue)
    {
        if (string.IsNullOrWhiteSpace(pathextValue))
        {
            return new[] { ".exe", ".cmd", ".bat" };
        }

        return pathextValue
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
            .ToArray();
    }
}
