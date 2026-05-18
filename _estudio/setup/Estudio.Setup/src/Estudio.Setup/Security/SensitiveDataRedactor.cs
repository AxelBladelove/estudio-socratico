using System.Text.RegularExpressions;

namespace Estudio.Setup.Security;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var redacted = value;
        redacted = GeminiApiKeyPattern().Replace(redacted, "[GEMINI_API_KEY_REDACTED]");
        redacted = GitHubClassicTokenPattern().Replace(redacted, "[GITHUB_TOKEN_REDACTED]");
        redacted = GitHubFineGrainedTokenPattern().Replace(redacted, "[GITHUB_TOKEN_REDACTED]");
        redacted = JsonApiKeyPattern().Replace(redacted, "$1[API_KEY_REDACTED]$3");
        redacted = ExercismTokenArgumentPattern().Replace(redacted, "$1[EXERCISM_TOKEN_REDACTED]");
        redacted = ExercismTokenAssignmentPattern().Replace(redacted, "$1[EXERCISM_TOKEN_REDACTED]");
        redacted = GenericBearerPattern().Replace(redacted, "$1[SECRET_REDACTED]");
        return redacted;
    }

    [GeneratedRegex("AIza[0-9A-Za-z\\-_]{20,}", RegexOptions.Compiled)]
    private static partial Regex GeminiApiKeyPattern();

    [GeneratedRegex("gh[pousr]_[0-9A-Za-z]{20,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GitHubClassicTokenPattern();

    [GeneratedRegex("github_pat_[0-9A-Za-z_]{20,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GitHubFineGrainedTokenPattern();

    [GeneratedRegex("(\"apiKey\"\\s*:\\s*\")(.*?)(\")", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex JsonApiKeyPattern();

    [GeneratedRegex("(--token\\s+)([^\\s\"]+|\".*?\")", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ExercismTokenArgumentPattern();

    [GeneratedRegex("(ESTUDIO_EXERCISM_TOKEN\\s*[=:]\\s*)([^\\r\\n]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ExercismTokenAssignmentPattern();

    [GeneratedRegex("(Bearer\\s+)([A-Za-z0-9._\\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GenericBearerPattern();
}