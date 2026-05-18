using Estudio.Setup.Services;

namespace Estudio.Setup.Core;

public sealed class SetupExecutionRouter
{
    private readonly Func<SetupOptions, string, string, ICommandRunner, ISetupProgressSink?, CancellationToken, Task<SetupRunArtifacts>> _legacyRun;
    private readonly Func<SetupOptions, string, string, ICommandRunner, IDesiredStateSetupProgressSink?, CancellationToken, Task<DesiredStateSetupRunArtifacts>> _desiredStateRun;

    public SetupExecutionRouter()
        : this(
            (options, workspaceRoot, studentAlias, commandRunner, progress, cancellationToken) => new SetupRunCoordinator()
                .RunAndPersistAsync(options, workspaceRoot, studentAlias, commandRunner, progress, cancellationToken),
            (options, workspaceRoot, studentAlias, commandRunner, progress, cancellationToken) => new DesiredStateSetupCoordinator()
                .RunAndPersistAsync(options, workspaceRoot, studentAlias, commandRunner, progress, cancellationToken))
    {
    }

    public SetupExecutionRouter(
        Func<SetupOptions, string, string, ICommandRunner, ISetupProgressSink?, CancellationToken, Task<SetupRunArtifacts>> legacyRun,
        Func<SetupOptions, string, string, ICommandRunner, IDesiredStateSetupProgressSink?, CancellationToken, Task<DesiredStateSetupRunArtifacts>> desiredStateRun)
    {
        _legacyRun = legacyRun;
        _desiredStateRun = desiredStateRun;
    }

    public async Task<SetupExecutionArtifacts> RunAndPersistAsync(
        SetupOptions options,
        string workspaceRoot,
        string studentAlias,
        ICommandRunner commandRunner,
        TextWriter? jsonWriter,
        CancellationToken cancellationToken)
    {
        if (options.Engine == SetupExecutionEngine.DesiredState)
        {
            var jsonProgress = options.JsonProgressRequested && jsonWriter is not null
                ? new DesiredStateJsonProgressSink(jsonWriter)
                : null;
            var artifacts = await _desiredStateRun(
                options,
                workspaceRoot,
                studentAlias,
                commandRunner,
                jsonProgress,
                cancellationToken);
            if (jsonProgress is not null)
            {
                await jsonProgress.WriteArtifactsAsync(artifacts, cancellationToken);
            }

            return SetupExecutionArtifacts.FromDesiredState(artifacts);
        }

        var legacyJsonProgress = options.JsonProgressRequested && jsonWriter is not null
            ? new JsonSetupProgressSink(jsonWriter)
            : null;
        var legacyArtifacts = await _legacyRun(
            options,
            workspaceRoot,
            studentAlias,
            commandRunner,
            legacyJsonProgress,
            cancellationToken);
        if (legacyJsonProgress is not null)
        {
            await legacyJsonProgress.WriteArtifactsAsync(legacyArtifacts, cancellationToken);
        }

        return SetupExecutionArtifacts.FromLegacy(legacyArtifacts);
    }
}