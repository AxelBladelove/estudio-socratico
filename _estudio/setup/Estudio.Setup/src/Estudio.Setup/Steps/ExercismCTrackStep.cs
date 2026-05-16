using System.Diagnostics;
using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class ExercismCTrackStep : ISetupStep
{
    public const string TokenEnvironmentVariable = "ESTUDIO_EXERCISM_TOKEN";
    public const string TokenUrl = "https://exercism.org/settings/api_cli";
    public const string CTrackUrl = "https://exercism.org/tracks/c";

    private readonly ICommandRunner _commandRunner;
    private readonly string _workspacePath;
    private readonly Func<string?> _tokenProvider;
    private readonly Action<string> _openUrl;

    public ExercismCTrackStep(
        ICommandRunner commandRunner,
        string? workspacePath = null,
        Func<string?>? tokenProvider = null,
        Action<string>? openUrl = null)
    {
        _commandRunner = commandRunner;
        _workspacePath = workspacePath ?? ResolveDefaultWorkspacePath();
        _tokenProvider = tokenProvider ?? (() => Environment.GetEnvironmentVariable(TokenEnvironmentVariable));
        _openUrl = openUrl ?? TryOpenUrl;
    }

    public string Id => "exercism-c-track";
    public string Name => "Exercism C track";

    public async Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var config = await ReadConfigAsync(cancellationToken);
        if (!config.WasStarted)
        {
            return StepResult.Missing("Exercism: CLI no esta disponible para configurar el track C.");
        }

        if (!HasConfiguredToken(config))
        {
            return StepResult.Missing($"Exercism: falta token. Abre {TokenUrl}, copia el token y pegalo en el instalador.");
        }

        return await VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureAndPrepareAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureAndPrepareAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return ConfigureAndPrepareAsync(cancellationToken);
    }

    public async Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var config = await ReadConfigAsync(cancellationToken);
        if (!config.WasStarted)
        {
            return StepResult.Missing("Exercism: CLI no esta disponible para verificar el track C.");
        }

        if (!HasConfiguredToken(config))
        {
            _openUrl(TokenUrl);
            return StepResult.Missing($"Exercism: falta token. Abre {TokenUrl}, copia el token y pegalo en el instalador.");
        }

        return await EnsureCTrackAsync(cancellationToken);
    }

    private async Task<StepResult> ConfigureAndPrepareAsync(CancellationToken cancellationToken)
    {
        var config = await ReadConfigAsync(cancellationToken);
        if (!config.WasStarted)
        {
            return StepResult.Missing("Exercism: CLI no esta disponible. Instala Exercism CLI y reintenta.");
        }

        var token = _tokenProvider()?.Trim();
        if (!HasConfiguredToken(config))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _openUrl(TokenUrl);
                return StepResult.Missing($"Exercism: falta token. Abre {TokenUrl}, copia el token y pegalo en el instalador.");
            }

            var configured = await ConfigureTokenAsync(token, cancellationToken);
            if (!configured.Success)
            {
                return configured;
            }
        }
        else if (!string.IsNullOrWhiteSpace(token))
        {
            var configured = await ConfigureTokenAsync(token, cancellationToken);
            if (!configured.Success)
            {
                return configured;
            }
        }

        var prepared = await _commandRunner.RunAsync("exercism", "prepare", cancellationToken);
        if (!prepared.WasStarted)
        {
            return StepResult.Missing("Exercism: CLI no esta disponible para preparar dependencias.");
        }

        if (!prepared.IsSuccess)
        {
            return StepResult.Fail("Exercism: `exercism prepare` no completo correctamente.");
        }

        return await EnsureCTrackAsync(cancellationToken);
    }

    private async Task<StepResult> ConfigureTokenAsync(string token, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(
            "exercism",
            $"configure --token {QuoteArgument(token)} --workspace {QuoteArgument(_workspacePath)}",
            cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing("Exercism: CLI no esta disponible para guardar el token.");
        }

        if (!result.IsSuccess)
        {
            _openUrl(TokenUrl);
            return StepResult.Missing($"Exercism: token invalido o no aceptado. Copia un token nuevo en {TokenUrl} y reintenta.");
        }

        return StepResult.Ok("Exercism: token y workspace configurados.");
    }

    private async Task<StepResult> EnsureCTrackAsync(CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(
            "exercism",
            "download --track c --exercise hello-world",
            cancellationToken);
        if (!result.WasStarted)
        {
            return StepResult.Missing("Exercism: CLI no esta disponible para descargar hello-world.");
        }

        if (result.IsSuccess || LooksLikeExistingExercise(result))
        {
            return StepResult.Ok("Exercism C: track listo y hello-world disponible.");
        }

        if (LooksLikeTrackNotJoined(result))
        {
            _openUrl(CTrackUrl);
            return StepResult.Missing($"Exercism C: falta unirse al track C. Abri {CTrackUrl}; pulsa Join the C Track y reintenta los fallidos.");
        }

        return StepResult.Fail($"Exercism C: no se pudo descargar hello-world. {FirstNonEmptyLine(SafeOutput(result))}");
    }

    private Task<CommandResult> ReadConfigAsync(CancellationToken cancellationToken)
    {
        return _commandRunner.RunAsync("exercism", "configure --show", cancellationToken);
    }

    private static bool HasConfiguredToken(CommandResult result)
    {
        var text = SafeOutput(result);
        if (text.Contains("There is no token configured", StringComparison.OrdinalIgnoreCase)
            || text.Contains("missing required user config: 'token'", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var tokenIndex = line.IndexOf("Token:", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex < 0)
            {
                continue;
            }

            var value = line[(tokenIndex + "Token:".Length)..].Trim();
            var closeParenIndex = value.LastIndexOf(')');
            if (closeParenIndex >= 0)
            {
                value = value[(closeParenIndex + 1)..].Trim();
            }

            return value.Length > 0;
        }

        return false;
    }

    private static bool LooksLikeExistingExercise(CommandResult result)
    {
        var text = SafeOutput(result);
        return text.Contains("already exists", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ya existe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTrackNotJoined(CommandResult result)
    {
        var text = SafeOutput(result);
        return text.Contains("track_not_joined", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not joined", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not enrolled", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unirse", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeOutput(CommandResult result)
    {
        return $"{result.StandardOutput}\n{result.StandardError}";
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

    private static string QuoteArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string ResolveDefaultWorkspacePath()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
        {
            profile = Environment.CurrentDirectory;
        }

        return Path.Combine(profile, "Exercism");
    }

    private static void TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // The visible URL in the setup message is the fallback.
        }
    }
}
