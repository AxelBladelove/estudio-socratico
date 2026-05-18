using Estudio.Setup.Profile;
using Estudio.Setup.Release;
using Estudio.Setup.Services;

namespace Estudio.Setup.Core;

public sealed class SetupApplicationHost
{
    private readonly string _appBaseDirectory;
    private readonly Func<ICommandRunner> _commandRunnerFactory;

    public SetupApplicationHost(string? appBaseDirectory = null, Func<ICommandRunner>? commandRunnerFactory = null)
    {
        _appBaseDirectory = appBaseDirectory ?? AppContext.BaseDirectory;
        _commandRunnerFactory = commandRunnerFactory ?? (() => new ProcessCommandRunner());
    }

    public SetupLaunchContext CreateLaunchContext(SetupOptions options, string currentDirectory)
    {
        var bootstrapRoot = LocalStudentProfile.FindWorkspaceRoot(currentDirectory);
        var studentAlias = options.AliasOverride ?? LocalStudentProfile.ResolveAlias(bootstrapRoot);
        LocalStudentProfile.ValidateAlias(studentAlias);
        var workspaceRoot = SetupLocationResolver.ResolveWorkspaceRoot(
            options,
            _appBaseDirectory,
            currentDirectory,
            studentAlias);

        return new SetupLaunchContext(
            options,
            currentDirectory,
            bootstrapRoot,
            workspaceRoot,
            studentAlias,
            _commandRunnerFactory());
    }

    public bool ShouldRunPackage(SetupOptions options)
    {
        return options.Mode == SetupMode.Package;
    }

    public bool ShouldRunTerminalGui(SetupOptions options)
    {
        return options.TuiRequested && !options.JsonProgressRequested && options.Engine == SetupExecutionEngine.Legacy;
    }

    public bool DesiredStateNeedsVisualHost(SetupOptions options)
    {
        return options.Engine == SetupExecutionEngine.DesiredState && options.TuiRequested && !options.JsonProgressRequested;
    }

    public Task<ReleasePackageResult> CreatePackageAsync(string bootstrapRoot, CancellationToken cancellationToken)
    {
        return new ReleasePackager(_commandRunnerFactory()).CreateAsync(
            ReleasePackager.ForWorkspace(bootstrapRoot),
            cancellationToken);
    }

    public Task<SetupExecutionArtifacts> RunAsync(
        SetupLaunchContext context,
        TextWriter? jsonWriter,
        CancellationToken cancellationToken)
    {
        return new SetupExecutionRouter().RunAndPersistAsync(
            context.Options,
            context.WorkspaceRoot,
            context.StudentAlias,
            context.CommandRunner,
            jsonWriter,
            cancellationToken);
    }
}