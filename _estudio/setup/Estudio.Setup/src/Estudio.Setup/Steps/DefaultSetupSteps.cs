using Estudio.Setup.Core;
using Estudio.Setup.Runtime;
using Estudio.Setup.Services;

namespace Estudio.Setup.Steps;

public static class DefaultSetupSteps
{
    public static IReadOnlyList<ISetupStep> Create(
        ICommandRunner commandRunner,
        string? appDataRoot = null,
        IGeminiRuntimeConfigProvider? geminiRuntimeConfigProvider = null,
        string studentAlias = "estudiante",
        string? workspaceRoot = null)
    {
        var resolvedWorkspaceRoot = workspaceRoot ?? Directory.GetCurrentDirectory();
        geminiRuntimeConfigProvider ??= new CompositeGeminiRuntimeConfigProvider(
            new FileGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveBundledRuntimeConfigPath()),
            new BootstrapGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveBundledRuntimeConfigBootstrapPath()),
            new FileGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveWorkspaceRuntimeConfigPath(resolvedWorkspaceRoot)),
            new BootstrapGeminiRuntimeConfigProvider(RuntimeConfigPaths.ResolveWorkspaceRuntimeConfigBootstrapPath(resolvedWorkspaceRoot)),
            new EnvironmentGeminiRuntimeConfigProvider());
        var codeCommand = VsCodeCliPathResolver.ResolveCodeCommand();

        return new ISetupStep[]
        {
            new WingetPackageStep("git", "Git", "Git.Git", "git", "--version", commandRunner),
            new WingetPackageStep("github-cli", "GitHub CLI", "GitHub.cli", "gh", "--version", commandRunner),
            new GitHubAuthStep(commandRunner),
            new GitIdentityStep(commandRunner, studentAlias),
            new GitSafetyBackupStep(commandRunner),
            new GitHubAliasRenameStep(commandRunner, resolvedWorkspaceRoot, studentAlias),
            new LocalAliasStep(resolvedWorkspaceRoot, studentAlias),
            new GitHubForkStep(commandRunner, studentAlias),
            new GitRemoteStep(commandRunner, studentAlias),
            new GitProjectUpdateStep(commandRunner),
            new WingetPackageStep("node", "Node.js", "OpenJS.NodeJS.LTS", "node", "--version", commandRunner),
            new WingetPackageStep("vscode", "Visual Studio Code", "Microsoft.VisualStudioCode", codeCommand, "--version", commandRunner),
            new VsCodeSettingsStep(
                VsCodeSettingsPaths.ResolveSettingsPath(appDataRoot),
                studentAlias,
                RuntimeConfigPaths.ResolveConfigPath(appDataRoot)),
            new VsixPackageStep(resolvedWorkspaceRoot, commandRunner),
            new VsixExtensionStep(
                VsixExtensionPaths.ResolveVsixPath(resolvedWorkspaceRoot),
                VsixExtensionPaths.ExtensionId,
                commandRunner,
                codeCommand),
            new WingetPackageStep("powershell7", "PowerShell 7", "Microsoft.PowerShell", "pwsh", "--version", commandRunner),
            new WingetPackageStep("exercism-cli", "Exercism CLI", "Exercism.CLI", "exercism", "version", commandRunner),
            new Msys2ToolchainStep(commandRunner),
            new UserPathStep(new UserEnvironment(), new[] { Msys2ToolchainStep.Ucrt64Bin }),
            new ExercismCTrackStep(commandRunner),
            new GeminiRuntimeConfigStep(
                RuntimeConfigPaths.ResolveConfigPath(appDataRoot),
                geminiRuntimeConfigProvider),
            new ExerciseCatalogStep(resolvedWorkspaceRoot),
        };
    }
}
