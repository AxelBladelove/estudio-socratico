using Estudio.Setup.Services;
using Estudio.Setup.State;

namespace Estudio.Setup.Core;

public sealed class DesiredStateSetupCoordinator
{
    private readonly Func<ICommandRunner, string, string, string?, IReadOnlyList<ISetupStateNode>> _nodeFactory;
    private readonly Func<ICommandRunner, CancellationToken, Task<string?>> _githubUserResolver;

    public DesiredStateSetupCoordinator()
        : this(
            (commandRunner, studentAlias, workspaceRoot, appDataRoot) => DefaultSetupStateNodes.Create(
                commandRunner,
                studentAlias,
                workspaceRoot,
                appDataRoot),
            ResolveGitHubUserAsync)
    {
    }

    public DesiredStateSetupCoordinator(
        Func<ICommandRunner, string, string, string?, IReadOnlyList<ISetupStateNode>> nodeFactory,
        Func<ICommandRunner, CancellationToken, Task<string?>> githubUserResolver)
    {
        _nodeFactory = nodeFactory;
        _githubUserResolver = githubUserResolver;
    }

    public async Task<DesiredStateSetupRunArtifacts> RunAndPersistAsync(
        SetupOptions options,
        string workspaceRoot,
        string studentAlias,
        ICommandRunner commandRunner,
        IDesiredStateSetupProgressSink? progress,
        CancellationToken cancellationToken)
    {
        var engine = new DesiredStateSetupEngine(
            _nodeFactory(
                commandRunner,
                studentAlias,
                workspaceRoot,
                null),
            progress ?? NullDesiredStateSetupProgressSink.Instance);
        var report = await engine.RunAsync(options, cancellationToken);
        var githubUser = await _githubUserResolver(commandRunner, cancellationToken);
        var metadata = SetupStateMetadata.ForWorkspace(studentAlias, workspaceRoot, githubUser);
        var stateRoot = SetupPathDefaults.ResolveStateRoot(options.StateRoot);
        var setupReport = report.ToSetupReport();
        var statePath = await new FileSetupStateStore(stateRoot)
            .SaveAsync(options, report, metadata, cancellationToken);
        var logPath = await new FileSetupLogWriter(SetupPathDefaults.ResolveLogRoot(stateRoot))
            .SaveAsync(options, studentAlias, setupReport, cancellationToken);
        var reportPath = await new DesiredStateSetupMarkdownReportWriter(stateRoot)
            .SaveAsync(options, studentAlias, report, cancellationToken);

        return new DesiredStateSetupRunArtifacts(report, statePath, logPath, reportPath, studentAlias);
    }

    private static async Task<string?> ResolveGitHubUserAsync(ICommandRunner commandRunner, CancellationToken cancellationToken)
    {
        var (user, _) = await Steps.GitHubUserResolver.ResolveAsync(commandRunner, cancellationToken);
        return user;
    }
}

public sealed record DesiredStateSetupRunArtifacts(
    DesiredStateSetupReport Report,
    string StatePath,
    string LogPath,
    string ReportPath,
    string Alias);