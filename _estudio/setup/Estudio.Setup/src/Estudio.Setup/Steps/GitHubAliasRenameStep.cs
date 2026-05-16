using Estudio.Setup.Core;
using Estudio.Setup.Profile;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public sealed class GitHubAliasRenameStep : ISetupStep
{
    private readonly ICommandRunner _commandRunner;
    private readonly string _workspaceRoot;
    private readonly string _newAlias;

    public GitHubAliasRenameStep(ICommandRunner commandRunner, string workspaceRoot, string newAlias)
    {
        _commandRunner = commandRunner;
        _workspaceRoot = workspaceRoot;
        _newAlias = newAlias;
    }

    public string Id => "github-alias-rename";
    public string Name => "GitHub alias rename";

    public async Task<StepResult> DetectAsync(SetupContext context, CancellationToken cancellationToken)
    {
        var oldAlias = await ReadCurrentAliasAsync(cancellationToken);
        if (oldAlias is null)
        {
            return StepResult.Ok("GitHub alias: no hay alias local previo que renombrar.");
        }

        if (string.Equals(oldAlias, _newAlias, StringComparison.Ordinal))
        {
            return StepResult.Ok($"GitHub alias: {_newAlias} no requiere renombrar fork.");
        }

        return StepResult.Missing($"GitHub alias: se debe revisar fork {oldAlias} -> {_newAlias}.");
    }

    public Task<StepResult> InstallAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureRenameAsync(cancellationToken);
    }

    public Task<StepResult> UpdateAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureRenameAsync(cancellationToken);
    }

    public Task<StepResult> RepairAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return EnsureRenameAsync(cancellationToken);
    }

    public Task<StepResult> VerifyAsync(SetupContext context, CancellationToken cancellationToken)
    {
        return VerifyRenameAsync(cancellationToken);
    }

    private async Task<StepResult> EnsureRenameAsync(CancellationToken cancellationToken)
    {
        LocalStudentProfile.ValidateAlias(_newAlias);
        var oldAlias = await ReadCurrentAliasAsync(cancellationToken);
        if (oldAlias is null)
        {
            return StepResult.Ok("GitHub alias: no hay alias local previo que renombrar.");
        }

        LocalStudentProfile.ValidateAlias(oldAlias);
        if (string.Equals(oldAlias, _newAlias, StringComparison.Ordinal))
        {
            return StepResult.Ok($"GitHub alias: {_newAlias} ya coincide con el alias local.");
        }

        var (user, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var oldFork = GitHubForkStep.BuildForkName(oldAlias);
        var newFork = GitHubForkStep.BuildForkName(_newAlias);
        var oldRepo = $"{user}/{oldFork}";
        var newRepo = $"{user}/{newFork}";

        var oldView = await _commandRunner.RunAsync("gh", $"repo view {oldRepo} --json name --jq .name", cancellationToken);
        if (!oldView.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para revisar el fork anterior.");
        }

        if (!oldView.IsSuccess)
        {
            return StepResult.Ok($"GitHub alias: no existe fork anterior {oldRepo}; el paso github-fork preparara {newRepo}.");
        }

        var newView = await _commandRunner.RunAsync("gh", $"repo view {newRepo} --json name --jq .name", cancellationToken);
        if (!newView.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para revisar el fork nuevo.");
        }

        if (newView.IsSuccess)
        {
            return StepResult.Ok($"GitHub alias: {newRepo} ya existe; no se renombra {oldRepo}.");
        }

        var rename = await _commandRunner.RunAsync(
            "gh",
            $"repo rename {newFork} --repo {oldRepo} --yes",
            cancellationToken);
        if (!rename.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para renombrar el fork.");
        }

        return rename.IsSuccess
            ? StepResult.Ok($"GitHub alias: fork renombrado {oldRepo} -> {newRepo}.")
            : StepResult.Fail($"GitHub alias: no se pudo renombrar {oldRepo}. {FirstNonEmptyLine(rename.StandardError)}");
    }

    private async Task<StepResult> VerifyRenameAsync(CancellationToken cancellationToken)
    {
        var oldAlias = await ReadCurrentAliasAsync(cancellationToken);
        if (oldAlias is null)
        {
            return StepResult.Ok("GitHub alias: no hay alias local previo que renombrar.");
        }

        if (string.Equals(oldAlias, _newAlias, StringComparison.Ordinal))
        {
            return StepResult.Ok($"GitHub alias: {_newAlias} no requiere renombrar fork.");
        }

        var (user, error) = await GitHubUserResolver.ResolveAsync(_commandRunner, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var oldFork = GitHubForkStep.BuildForkName(oldAlias);
        var newFork = GitHubForkStep.BuildForkName(_newAlias);
        var oldRepo = $"{user}/{oldFork}";
        var newRepo = $"{user}/{newFork}";
        var newView = await _commandRunner.RunAsync("gh", $"repo view {newRepo} --json name --jq .name", cancellationToken);
        if (!newView.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para verificar el fork renombrado.");
        }

        if (newView.IsSuccess)
        {
            return StepResult.Ok($"GitHub alias: {newRepo} disponible.");
        }

        var oldView = await _commandRunner.RunAsync("gh", $"repo view {oldRepo} --json name --jq .name", cancellationToken);
        if (!oldView.WasStarted)
        {
            return StepResult.Missing("GitHub: gh no esta disponible para verificar el fork anterior.");
        }

        return oldView.IsSuccess
            ? StepResult.Missing($"GitHub alias: {oldRepo} sigue existiendo y {newRepo} no esta disponible.")
            : StepResult.Ok($"GitHub alias: no existe fork anterior {oldRepo}; el paso github-fork preparara {newRepo}.");
    }

    private async Task<string?> ReadCurrentAliasAsync(CancellationToken cancellationToken)
    {
        var path = LocalAliasStep.ResolveIdentityPath(_workspaceRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        var current = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        return string.IsNullOrWhiteSpace(current) ? null : current;
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
