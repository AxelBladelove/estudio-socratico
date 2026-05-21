using System.Text.RegularExpressions;

namespace EstudioSocratico.Configurator.Core;

public static partial class SecretRedactor
{
    private const string Redacted = "[REDACTED]";

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var text = value;
        text = GitHubTokenRegex().Replace(text, Redacted);
        text = ExercismTokenArgumentRegex().Replace(text, m => $"{m.Groups[1].Value}{Redacted}");
        text = TokenLineRegex().Replace(text, m => $"{m.Groups[1].Value}{Redacted}");
        text = SensitiveUuidRegex().Replace(text, Redacted);
        text = ExercismTokenRegex().Replace(text, m => $"{m.Groups[1].Value}{Redacted}");
        text = GenericTokenAssignmentRegex().Replace(text, m => $"{m.Groups[1].Value}{Redacted}");
        text = AuthorizationHeaderRegex().Replace(text, m => $"{m.Groups[1].Value}{Redacted}");
        return text;
    }

    public static IReadOnlyList<string> RedactArguments(IEnumerable<string> arguments)
    {
        var result = new List<string>();
        var redactNext = false;

        foreach (var arg in arguments)
        {
            if (redactNext)
            {
                result.Add(Redacted);
                redactNext = false;
                continue;
            }

            var lower = arg.ToLowerInvariant();
            if (lower is "--token" or "-t" or "--password" or "--pat")
            {
                result.Add(arg);
                redactNext = true;
                continue;
            }

            result.Add(Redact(arg));
        }

        return result;
    }

    [GeneratedRegex(@"gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}", RegexOptions.Compiled)]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"(?i)(exercism\s+configure\s+--token(?:=|\s+))[^\s]+", RegexOptions.Compiled)]
    private static partial Regex ExercismTokenRegex();

    [GeneratedRegex(@"(?i)(--token(?:=|\s+))[^\s]+", RegexOptions.Compiled)]
    private static partial Regex ExercismTokenArgumentRegex();

    [GeneratedRegex(@"(?im)^(\s*.*token\b.*?)(?:[:=]\s*)?[^:\r\n]*$", RegexOptions.Compiled)]
    private static partial Regex TokenLineRegex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled)]
    private static partial Regex SensitiveUuidRegex();

    [GeneratedRegex(@"(?i)([""']?(?:token|password|secret|apikey|api_key|authorization)[""']?\s*[:=]\s*[""']?)[^""'\s,;]+", RegexOptions.Compiled)]
    private static partial Regex GenericTokenAssignmentRegex();

    [GeneratedRegex(@"(?i)(authorization:\s*(?:bearer|token)\s+)[^\r\n]+", RegexOptions.Compiled)]
    private static partial Regex AuthorizationHeaderRegex();
}

public static class PathSafety
{
    public static string RequireInside(string root, string candidate, string operation)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
        var candidateFull = Path.GetFullPath(candidate);

        if (!candidateFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{operation} rejected an unsafe path outside the allowed root: {candidateFull}");
        }

        return candidateFull;
    }

    public static bool IsInside(string root, string candidate)
    {
        try
        {
            _ = RequireInside(root, candidate, "path check");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
