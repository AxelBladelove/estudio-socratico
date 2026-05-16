using Estudio.Setup.Services;
using Estudio.Setup.State;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Core;

public sealed class SetupRunCoordinator
{
    private readonly Func<ICommandRunner, string, string, IReadOnlyList<ISetupStep>> _stepFactory;
    private readonly Func<ICommandRunner, CancellationToken, Task<string?>> _githubUserResolver;

    public SetupRunCoordinator()
        : this(
            (commandRunner, studentAlias, workspaceRoot) => DefaultSetupSteps.Create(
                commandRunner,
                studentAlias: studentAlias,
                workspaceRoot: workspaceRoot),
            ResolveGitHubUserAsync)
    {
    }

    public SetupRunCoordinator(
        Func<ICommandRunner, string, string, IReadOnlyList<ISetupStep>> stepFactory,
        Func<ICommandRunner, CancellationToken, Task<string?>> githubUserResolver)
    {
        _stepFactory = stepFactory;
        _githubUserResolver = githubUserResolver;
    }

    public async Task<SetupRunArtifacts> RunAndPersistAsync(
        SetupOptions options,
        string workspaceRoot,
        string studentAlias,
        ICommandRunner commandRunner,
        ISetupProgressSink? progress,
        CancellationToken cancellationToken)
    {
        var engine = new SetupEngine(
            _stepFactory(commandRunner, studentAlias, workspaceRoot),
            progress ?? NullSetupProgressSink.Instance);
        var report = await engine.RunAsync(options, cancellationToken);
        var githubUser = await _githubUserResolver(commandRunner, cancellationToken);
        var metadata = SetupStateMetadata.ForWorkspace(studentAlias, workspaceRoot, githubUser);
        var stateRoot = SetupPathDefaults.ResolveStateRoot(options.StateRoot);
        var statePath = await new FileSetupStateStore(stateRoot)
            .SaveAsync(options, report, metadata, cancellationToken);
        var logPath = await new FileSetupLogWriter(SetupPathDefaults.ResolveLogRoot(stateRoot))
            .SaveAsync(options, studentAlias, report, cancellationToken);
        var reportPath = await new FileSetupMarkdownReportWriter(stateRoot)
            .SaveAsync(options, studentAlias, report, cancellationToken);

        return new SetupRunArtifacts(report, statePath, logPath, reportPath, studentAlias);
    }

    private static async Task<string?> ResolveGitHubUserAsync(ICommandRunner commandRunner, CancellationToken cancellationToken)
    {
        var (user, _) = await GitHubUserResolver.ResolveAsync(commandRunner, cancellationToken);
        return user;
    }
}

public sealed record SetupRunArtifacts(
    SetupReport Report,
    string StatePath,
    string LogPath,
    string ReportPath,
    string Alias);
