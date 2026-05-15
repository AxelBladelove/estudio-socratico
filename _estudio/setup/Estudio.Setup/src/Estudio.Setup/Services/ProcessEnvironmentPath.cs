namespace Estudio.Setup.Services;

public static class ProcessEnvironmentPath
{
    public static string Merge(string? processPath, string? userPath)
    {
        var entries = new List<string>();
        AddEntries(entries, userPath);
        AddEntries(entries, processPath);
        return string.Join(Path.PathSeparator, entries);
    }

    private static void AddEntries(List<string> entries, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        foreach (var entry in path.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!entries.Any(existing => SamePath(existing, entry)))
            {
                entries.Add(entry);
            }
        }
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(
            Normalize(left),
            Normalize(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return path.Trim().TrimEnd('\\', '/');
    }
}
