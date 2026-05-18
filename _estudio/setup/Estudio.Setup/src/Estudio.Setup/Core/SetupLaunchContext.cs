using Estudio.Setup.Services;

namespace Estudio.Setup.Core;

public sealed record SetupLaunchContext(
    SetupOptions Options,
    string CurrentDirectory,
    string BootstrapRoot,
    string WorkspaceRoot,
    string StudentAlias,
    ICommandRunner CommandRunner);