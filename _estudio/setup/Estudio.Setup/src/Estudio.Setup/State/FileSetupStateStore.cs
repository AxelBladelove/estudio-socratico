using System.Text.Json;
using Estudio.Setup.Core;

namespace Estudio.Setup.State;

public sealed class FileSetupStateStore
{
    public const string CurrentSetupVersion = "2.0.0-rc5";

    private readonly string _stateRoot;

    public FileSetupStateStore(string stateRoot)
    {
        _stateRoot = stateRoot;
    }

    public async Task<string> SaveAsync(SetupOptions options, SetupReport report, CancellationToken cancellationToken)
    {
        return await SaveAsync(options, report, SetupStateMetadata.Empty, cancellationToken);
    }

    public async Task<string> SaveAsync(
        SetupOptions options,
        SetupReport report,
        SetupStateMetadata metadata,
        CancellationToken cancellationToken)
    {
        return await SaveAsync(
            options,
            metadata,
            report.LastSuccessfulStep,
            BuildComponentState(report),
            cancellationToken);
    }

    public async Task<string> SaveAsync(
        SetupOptions options,
        DesiredStateSetupReport report,
        SetupStateMetadata metadata,
        CancellationToken cancellationToken)
    {
        var summary = SetupExecutionSummary.FromDesiredState(report);
        return await SaveAsync(
            options,
            metadata,
            SetupExecutionSummary.LastSuccessfulDesiredStateBlockId(report),
            summary.ToDictionary(block => block.Id, block => ToState(block.Status), StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }

    private async Task<string> SaveAsync(
        SetupOptions options,
        SetupStateMetadata metadata,
        string lastSuccessfulStep,
        IReadOnlyDictionary<string, string> installedComponents,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_stateRoot);
        var path = Path.Combine(_stateRoot, "setup-state.json");
        var storedComponents = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in installedComponents)
        {
            storedComponents[component.Key] = component.Value;
        }

        var state = new PersistedSetupState(
            setupVersion: CurrentSetupVersion,
            engine: options.Engine == SetupExecutionEngine.DesiredState ? "desired-state" : "legacy",
            alias: metadata.Alias,
            githubUser: metadata.GithubUser,
            forkOwner: metadata.ForkOwner,
            forkName: metadata.ForkName,
            upstream: metadata.Upstream,
            workspace: metadata.Workspace,
            mode: options.Mode.ToString(),
            lastSuccessfulStep: lastSuccessfulStep,
            installedComponents: storedComponents);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            state,
            new JsonSerializerOptions { WriteIndented = true },
            cancellationToken);

        return path;
    }

    private static SortedDictionary<string, string> BuildComponentState(SetupReport report)
    {
        var state = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var execution in report.Steps)
        {
            state[execution.StepId] = ToState(execution.Result);
        }

        return state;
    }

    private static string ToState(StepResult result)
    {
        if (result.IsWarning)
        {
            return "warning";
        }

        if (result.IsMissing)
        {
            return "missing";
        }

        return result.Success ? "ok" : "failed";
    }

    private static string ToState(SetupExecutionBlockStatus status)
    {
        return status switch
        {
            SetupExecutionBlockStatus.Ready => "ok",
            SetupExecutionBlockStatus.Applied => "applied",
            SetupExecutionBlockStatus.Repaired => "repaired",
            SetupExecutionBlockStatus.Pending => "missing",
            SetupExecutionBlockStatus.Warning => "warning",
            SetupExecutionBlockStatus.Failed => "failed",
            _ => "failed",
        };
    }

    private sealed record PersistedSetupState(
        string setupVersion,
        string engine,
        string? alias,
        string? githubUser,
        string? forkOwner,
        string? forkName,
        string? upstream,
        string? workspace,
        string mode,
        string lastSuccessfulStep,
        SortedDictionary<string, string> installedComponents);
}

public sealed record SetupStateMetadata(
    string? Alias,
    string? GithubUser,
    string? ForkOwner,
    string? ForkName,
    string? Upstream,
    string? Workspace)
{
    public static SetupStateMetadata Empty { get; } = new(null, null, null, null, null, null);

    public static SetupStateMetadata ForWorkspace(string alias, string workspace, string? githubUser = null)
    {
        return new SetupStateMetadata(
            Alias: alias,
            GithubUser: githubUser,
            ForkOwner: githubUser,
            ForkName: $"estudio-socratico-{alias}",
            Upstream: "AxelBladelove/estudio-socratico",
            Workspace: workspace);
    }
}
