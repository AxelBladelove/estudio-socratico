using System.Text.Json;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class LogManager(AppPaths paths) : IProgressSink
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string InstallerLogPath => Path.Combine(paths.LogsRoot, "installer.log");
    public string ErrorsLogPath => Path.Combine(paths.LogsRoot, "errors.log");
    public string LastRunPath => Path.Combine(paths.LogsRoot, "last-run.json");
    public string DiagnosticsPath => Path.Combine(paths.LogsRoot, "diagnostics.json");

    public async Task StartRunAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureBaseDirectories();
        var payload = new
        {
            product = ProductInfo.DisplayName,
            version = ProductInfo.Version,
            startedAtUtc = DateTimeOffset.UtcNow,
            os = Environment.OSVersion.VersionString,
            user = Environment.UserName
        };
        await File.WriteAllTextAsync(LastRunPath, JsonSerializer.Serialize(payload, JsonDefaults.Options), cancellationToken)
            .ConfigureAwait(false);
        await WriteAsync("info", "run", "Configurador iniciado.", cancellationToken).ConfigureAwait(false);
    }

    public Task ReportAsync(ProgressEvent progress, CancellationToken cancellationToken = default)
    {
        return WriteAsync("progress", progress.StepId, $"{progress.Title}: {progress.Message}", cancellationToken);
    }

    public async Task WriteCommandAsync(CommandSpec spec, CommandResult? result, CancellationToken cancellationToken = default)
    {
        var arguments = string.Join(" ", SecretRedactor.RedactArguments(spec.Arguments));
        var message = result is null
            ? $"$ {spec.FileName} {arguments}"
            : $"$ {spec.FileName} {arguments} -> exit {result.ExitCode} in {result.Duration.TotalMilliseconds:n0}ms";
        await WriteAsync("command", "command", message, cancellationToken).ConfigureAwait(false);

        if (result is not null)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                await WriteAsync("stdout", "command", result.StandardOutput, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                await WriteAsync("stderr", "command", result.StandardError, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task WriteErrorAsync(InstallerError error, CancellationToken cancellationToken = default)
    {
        await WriteAsync("error", error.Code.ToString(), $"{error.Title}: {error.Description}", cancellationToken)
            .ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                atUtc = DateTimeOffset.UtcNow,
                version = ProductInfo.Version,
                error = error with { TechnicalDetails = SecretRedactor.Redact(error.TechnicalDetails) }
            }, JsonDefaults.Options);
            await File.AppendAllTextAsync(ErrorsLogPath, line + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteDiagnosticsAsync(DiagnosticsReport report, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.Options);
        await File.WriteAllTextAsync(DiagnosticsPath, SecretRedactor.Redact(json), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task WriteAsync(string level, string step, string message, CancellationToken cancellationToken = default)
    {
        paths.EnsureBaseDirectories();
        var line = JsonSerializer.Serialize(new
        {
            atUtc = DateTimeOffset.UtcNow,
            version = ProductInfo.Version,
            level,
            step,
            message = SecretRedactor.Redact(message)
        }, JsonDefaults.Options);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(InstallerLogPath, line + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class ManifestManager(AppPaths paths)
{
    public async Task<InstallerManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureBaseDirectories();
        if (!File.Exists(paths.ManifestPath))
        {
            return new InstallerManifest();
        }

        var json = await File.ReadAllTextAsync(paths.ManifestPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<InstallerManifest>(json, JsonDefaults.Options) ?? new InstallerManifest();
    }

    public async Task SaveAsync(InstallerManifest manifest, CancellationToken cancellationToken = default)
    {
        paths.EnsureBaseDirectories();
        var next = manifest with { UpdatedAtUtc = DateTimeOffset.UtcNow };
        var json = JsonSerializer.Serialize(next, JsonDefaults.Options);
        await File.WriteAllTextAsync(paths.ManifestPath, SecretRedactor.Redact(json), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RecordDependencyAsync(
        DependencyState before,
        DependencyState after,
        bool installedByEstudio,
        string? installerSource,
        string? sha256,
        CancellationToken cancellationToken = default)
    {
        var manifest = await LoadAsync(cancellationToken).ConfigureAwait(false);
        manifest.Dependencies[after.Id] = new DependencyManifestEntry
        {
            Id = after.Id,
            DisplayName = after.DisplayName,
            VersionBefore = before.Version,
            VersionAfter = after.Version,
            PathBefore = before.Path,
            PathAfter = after.Path,
            ExistedBefore = before.Status == DependencyStatus.Ready,
            InstalledByEstudio = installedByEstudio,
            InstallerSource = installerSource,
            Sha256 = sha256
        };

        if (installedByEstudio && after.Path is { Length: > 0 })
        {
            manifest.SafeToRemove.Add(after.Path);
        }

        await SaveAsync(manifest, cancellationToken).ConfigureAwait(false);
    }
}
