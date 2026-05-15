using Estudio.Setup.Core;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class UserPathStep : ISetupStep
{
    private readonly IUserEnvironment _environment;
    private readonly IReadOnlyList<string> _requiredEntries;

    public UserPathStep(IUserEnvironment environment, IReadOnlyList<string> requiredEntries)
    {
        _environment = environment;
        _requiredEntries = requiredEntries;
    }

    public string Id => "user-path";
    public string Name => "User PATH";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyAsync(context, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsurePathAsync();
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsurePathAsync();
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsurePathAsync();
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var entries = ReadEntries();
        var missing = _requiredEntries.Where(required => !ContainsEntry(entries, required)).ToArray();
        if (missing.Length == 0)
        {
            return Task.FromResult(StepResult.Ok("PATH: entradas requeridas listas."));
        }

        return Task.FromResult(StepResult.Missing($"PATH: faltan entradas {string.Join(", ", missing)}."));
    }

    private Task<StepResult> EnsurePathAsync()
    {
        var existingEntries = ReadEntries().ToList();
        var entries = new List<string>();
        foreach (var required in _requiredEntries)
        {
            AddIfMissing(entries, required);
        }

        foreach (var existing in existingEntries)
        {
            AddIfMissing(entries, existing);
        }

        _environment.SetUserVariable("PATH", string.Join(Path.PathSeparator, entries));
        return Task.FromResult(StepResult.Ok("PATH: entradas requeridas agregadas al PATH de usuario."));
    }

    private IReadOnlyList<string> ReadEntries()
    {
        var path = _environment.GetUserVariable("PATH") ?? string.Empty;
        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static bool ContainsEntry(IEnumerable<string> entries, string required)
    {
        return entries.Any(entry => string.Equals(
            Normalize(entry),
            Normalize(required),
            StringComparison.OrdinalIgnoreCase));
    }

    private static void AddIfMissing(List<string> entries, string entry)
    {
        if (!ContainsEntry(entries, entry))
        {
            entries.Add(entry);
        }
    }

    private static string Normalize(string path)
    {
        return path.Trim().TrimEnd('\\', '/');
    }
}
