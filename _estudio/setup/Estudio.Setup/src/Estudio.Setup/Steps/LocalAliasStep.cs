using Estudio.Setup.Core;
using Estudio.Setup.Profile;

namespace Estudio.Setup.Steps;

public sealed class LocalAliasStep : ISetupStep
{
    private readonly string _workspaceRoot;
    private readonly string _alias;
    private readonly Func<DateTimeOffset> _now;

    public LocalAliasStep(string workspaceRoot, string alias, Func<DateTimeOffset>? now = null)
    {
        _workspaceRoot = workspaceRoot;
        _alias = alias;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public string Id => "local-alias";
    public string Name => "Local alias";

    public Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckAliasAsync(treatInvalidAsRepairable: true, cancellationToken);
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteAliasAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteAliasAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return WriteAliasAsync(cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return CheckAliasAsync(treatInvalidAsRepairable: false, cancellationToken);
    }

    private async Task<StepResult> CheckAliasAsync(bool treatInvalidAsRepairable, CancellationToken cancellationToken)
    {
        var path = ResolveIdentityPath(_workspaceRoot);
        if (!File.Exists(path))
        {
            return StepResult.Missing($"Alias: no existe {path}.");
        }

        var current = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        try
        {
            LocalStudentProfile.ValidateAlias(current);
        }
        catch (ArgumentException ex)
        {
            if (treatInvalidAsRepairable)
            {
                return StepResult.Missing($"Alias: .estudio_usuario contiene un alias invalido; se debe reescribir. {ex.Message}");
            }

            return StepResult.Fail($"Alias: .estudio_usuario contiene un alias invalido. {ex.Message}");
        }

        return string.Equals(current, _alias, StringComparison.Ordinal)
            ? StepResult.Ok($"Alias: {_alias}.")
            : StepResult.Missing($"Alias: .estudio_usuario contiene '{current}', se esperaba '{_alias}'.");
    }

    private async Task<StepResult> WriteAliasAsync(CancellationToken cancellationToken)
    {
        LocalStudentProfile.ValidateAlias(_alias);
        Directory.CreateDirectory(_workspaceRoot);
        var path = ResolveIdentityPath(_workspaceRoot);
        if (File.Exists(path))
        {
            var current = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
            if (string.Equals(current, _alias, StringComparison.Ordinal))
            {
                return StepResult.Ok($"Alias: {_alias} ya estaba configurado.");
            }

            File.Copy(path, ResolveBackupPath(path), overwrite: false);
        }

        await File.WriteAllTextAsync(path, _alias + Environment.NewLine, cancellationToken);
        return StepResult.Ok($"Alias: {_alias} escrito en {path}.");
    }

    public static string ResolveIdentityPath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, ".estudio_usuario");
    }

    private string ResolveBackupPath(string path)
    {
        var stamp = _now().UtcDateTime.ToString("yyyyMMddHHmmss");
        var backupPath = $"{path}.{stamp}.bak";
        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        for (var index = 1; ; index++)
        {
            var candidate = $"{path}.{stamp}.{index}.bak";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
