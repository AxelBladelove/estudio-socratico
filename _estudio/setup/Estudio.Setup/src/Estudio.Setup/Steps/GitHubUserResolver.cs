using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public static class GitHubUserResolver
{
    public static async Task<(string? User, StepResult? Error)> ResolveAsync(
        ICommandRunner commandRunner,
        CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync("gh", "api user --jq .login", cancellationToken);
        if (!result.WasStarted)
        {
            return (null, StepResult.Missing("GitHub: gh no esta disponible para leer el usuario autenticado."));
        }

        if (!result.IsSuccess)
        {
            return (null, StepResult.Missing($"GitHub: no se pudo leer usuario autenticado. {FirstNonEmptyLine(result.StandardError)}"));
        }

        var user = FirstNonEmptyLine(result.StandardOutput);
        if (string.IsNullOrWhiteSpace(user))
        {
            return (null, StepResult.Missing("GitHub: gh api user no devolvio usuario."));
        }

        return (user, null);
    }

    private static string FirstNonEmptyLine(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim();
            }
        }

        return string.Empty;
    }
}
