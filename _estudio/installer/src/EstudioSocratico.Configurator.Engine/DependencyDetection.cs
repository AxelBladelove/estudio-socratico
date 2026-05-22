using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class DependencyDetector(
    ICommandRunner runner,
    Func<VSCodePaths>? locateVSCode = null,
    string? managedToolsDirectory = null,
    Func<string, bool>? fileExists = null)
{
    private readonly Func<string, bool> _fileExists = fileExists ?? File.Exists;
    public static IReadOnlyList<DependencyRequirement> Requirements { get; } =
    [
        new(DependencyId.Winget, "WinGet", "winget", null, "1.8", Required: false),
        new(DependencyId.NodeJs, "Node.js LTS", "node", "OpenJS.NodeJS.LTS", "20.0"),
        new(DependencyId.Python, "Python", "python", "Python.Python.3.13", "3.10"),
        new(DependencyId.Git, "Git", "git", "Git.Git", "2.40"),
        new(DependencyId.GitHubCli, "GitHub CLI", "gh", "GitHub.cli", "2.40"),
        new(DependencyId.ExercismCli, "Exercism CLI", "exercism", "Exercism.CLI", "3.0"),
        new(DependencyId.VSCode, "Visual Studio Code", "code", "Microsoft.VisualStudioCode", "1.85"),
        new(DependencyId.Msys2, "MSYS2", "bash", "MSYS2.MSYS2", null),
        new(DependencyId.Gcc, "GCC UCRT64", "gcc", null, "13.0", ManagedThroughMsys2: true),
        new(DependencyId.Make, "Make", "make", null, "4.0", ManagedThroughMsys2: true)
    ];

    public async Task<IReadOnlyList<DependencyState>> DetectAllAsync(CancellationToken cancellationToken = default)
    {
        var states = new List<DependencyState>();
        foreach (var requirement in Requirements)
        {
            states.Add(await DetectAsync(requirement, cancellationToken).ConfigureAwait(false));
        }

        return states;
    }

    public async Task<DependencyState> DetectAsync(DependencyRequirement requirement, CancellationToken cancellationToken = default)
    {
        return requirement.Id switch
        {
            DependencyId.Msys2 => DetectMsys2(),
            DependencyId.Gcc => await DetectCommandAsync(requirement, ["--version"], ProductInfo.DefaultMsys2UcrtBin, cancellationToken).ConfigureAwait(false),
            DependencyId.Make => await DetectCommandAsync(requirement, ["--version"], ProductInfo.DefaultMsys2UcrtBin, cancellationToken).ConfigureAwait(false),
            DependencyId.Winget => await DetectCommandAsync(requirement, ["--info"], null, cancellationToken).ConfigureAwait(false),
            DependencyId.VSCode => await DetectVSCodeAsync(requirement, cancellationToken).ConfigureAwait(false),
            DependencyId.ExercismCli => await DetectCommandAsync(requirement, ["help"], managedToolsDirectory, cancellationToken).ConfigureAwait(false),
            _ => await DetectCommandAsync(requirement, ["--version"], null, cancellationToken).ConfigureAwait(false)
        };
    }

    private DependencyState DetectMsys2()
    {
        var bash = Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "bash.exe");
        var pacman = Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "pacman.exe");
        if (_fileExists(bash) && _fileExists(pacman))
        {
            return new DependencyState
            {
                Id = DependencyId.Msys2,
                DisplayName = "MSYS2",
                Status = DependencyStatus.Ready,
                Path = ProductInfo.DefaultMsys2Root,
                Source = "filesystem"
            };
        }

        return new DependencyState
        {
            Id = DependencyId.Msys2,
            DisplayName = "MSYS2",
            Status = DependencyStatus.Missing,
            Recommendation = "Instalar MSYS2 en C:\\msys64 y usar UCRT64."
        };
    }

    private async Task<DependencyState> DetectCommandAsync(
        DependencyRequirement requirement,
        IReadOnlyList<string> versionArgs,
        string? preferredDirectory,
        CancellationToken cancellationToken)
    {
        var commandPath = await ResolveCommandPathAsync(requirement.CommandName, preferredDirectory, cancellationToken).ConfigureAwait(false);
        if (commandPath is null)
        {
            return new DependencyState
            {
                Id = requirement.Id,
                DisplayName = requirement.DisplayName,
                Status = DependencyStatus.Missing,
                Recommendation = $"Instalar o reparar {requirement.DisplayName}."
            };
        }

        var result = await runner.RunAsync(new CommandSpec
        {
            FileName = commandPath,
            Arguments = versionArgs,
            Timeout = TimeSpan.FromSeconds(30),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return new DependencyState
            {
                Id = requirement.Id,
                DisplayName = requirement.DisplayName,
                Status = DependencyStatus.Broken,
                Path = commandPath,
                Error = new InstallerError
                {
                    Code = InstallerErrorCode.COMMAND_FAILED,
                    Title = $"{requirement.DisplayName} no responde",
                    Description = result.StandardError,
                    ProbableCause = "La instalacion existe pero el ejecutable fallo.",
                    RecommendedAction = "Ejecuta Reparar para reinstalar o corregir PATH."
                }
            };
        }

        var text = requirement.Id == DependencyId.VSCode
            ? result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : result.StandardOutput + Environment.NewLine + result.StandardError;
        var version = VersionParsing.FirstVersionLikeValue(text);
        var status = IsOutdated(version, requirement.MinimumVersion) ? DependencyStatus.Outdated : DependencyStatus.Ready;
        return new DependencyState
        {
            Id = requirement.Id,
            DisplayName = requirement.DisplayName,
            Status = status,
            Path = commandPath,
            Version = version,
            Source = "command",
            Recommendation = status == DependencyStatus.Outdated
                ? $"Actualizar {requirement.DisplayName} a {requirement.MinimumVersion} o superior."
                : null
        };
    }

    private async Task<DependencyState> DetectVSCodeAsync(DependencyRequirement requirement, CancellationToken cancellationToken)
    {
        var paths = locateVSCode?.Invoke() ?? VSCodeLocator.Resolve();
        if (!paths.HasCodeExe)
        {
            return new DependencyState
            {
                Id = requirement.Id,
                DisplayName = requirement.DisplayName,
                Status = DependencyStatus.Missing,
                Recommendation = "Instalar o reparar Visual Studio Code."
            };
        }

        if (!paths.HasCodeCmd)
        {
            return new DependencyState
            {
                Id = requirement.Id,
                DisplayName = requirement.DisplayName,
                Status = DependencyStatus.Broken,
                Path = paths.CodeExe,
                Error = new InstallerError
                {
                    Code = InstallerErrorCode.VSCODE_NOT_FOUND,
                    Title = "VS Code CLI no encontrado",
                    Description = "Code.exe existe, pero code.cmd no esta disponible.",
                    ProbableCause = "La instalacion de VS Code quedo incompleta o el directorio bin no se instalo.",
                    RecommendedAction = "Ejecuta Reparar para reinstalar VS Code."
                }
            };
        }

        var result = await runner.RunAsync(VSCodeLocator.BuildCodeCmdCommand(
            paths.CodeCmd!,
            ["--version"],
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromSeconds(30)), cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return new DependencyState
            {
                Id = requirement.Id,
                DisplayName = requirement.DisplayName,
                Status = DependencyStatus.Broken,
                Path = paths.CodeExe,
                Error = new InstallerError
                {
                    Code = InstallerErrorCode.VSCODE_NOT_FOUND,
                    Title = "VS Code CLI no responde",
                    Description = result.StandardError,
                    ProbableCause = "Code.exe existe, pero code.cmd no pudo ejecutarse correctamente.",
                    RecommendedAction = "Cierra VS Code y ejecuta Reparar."
                }
            };
        }

        var version = VersionParsing.FirstVersionLikeValue(
            result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
        var status = IsOutdated(version, requirement.MinimumVersion) ? DependencyStatus.Outdated : DependencyStatus.Ready;
        return new DependencyState
        {
            Id = requirement.Id,
            DisplayName = requirement.DisplayName,
            Status = status,
            Path = paths.CodeExe,
            Version = version,
            Source = "filesystem",
            Recommendation = status == DependencyStatus.Outdated
                ? $"Actualizar {requirement.DisplayName} a {requirement.MinimumVersion} o superior."
                : null
        };
    }

    public async Task<string?> ResolveCommandPathAsync(string command, string? preferredDirectory, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(preferredDirectory))
        {
            var exe = Path.Combine(preferredDirectory, command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? command : command + ".exe");
            if (_fileExists(exe))
            {
                return exe;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var commonPath = FindInCommonWindowsPaths(command);
            if (commonPath != null)
            {
                return commonPath;
            }

            TryRefreshProcessPath();
        }

        var where = await runner.RunAsync(new CommandSpec
        {
            FileName = "where.exe",
            Arguments = [command],
            Timeout = TimeSpan.FromSeconds(10),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!where.Succeeded)
        {
            return null;
        }

        return where.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(_fileExists);
    }

    private string? FindInCommonWindowsPaths(string command)
    {
        var cmdName = command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? command : command + ".exe";
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var pathsToCheck = new List<string>();

        if (string.Equals(command, "gh", StringComparison.OrdinalIgnoreCase))
        {
            pathsToCheck.Add(Path.Combine(programFiles, "GitHub CLI", cmdName));
            pathsToCheck.Add(Path.Combine(programFilesX86, "GitHub CLI", cmdName));
            pathsToCheck.Add(Path.Combine(localAppData, "Programs", "GitHub CLI", cmdName));
        }
        else if (string.Equals(command, "git", StringComparison.OrdinalIgnoreCase))
        {
            pathsToCheck.Add(Path.Combine(programFiles, "Git", "cmd", cmdName));
            pathsToCheck.Add(Path.Combine(programFiles, "Git", "bin", cmdName));
            pathsToCheck.Add(Path.Combine(programFilesX86, "Git", "cmd", cmdName));
            pathsToCheck.Add(Path.Combine(localAppData, "Programs", "Git", "cmd", cmdName));
        }
        else if (string.Equals(command, "node", StringComparison.OrdinalIgnoreCase))
        {
            pathsToCheck.Add(Path.Combine(programFiles, "nodejs", cmdName));
            pathsToCheck.Add(Path.Combine(programFilesX86, "nodejs", cmdName));
            pathsToCheck.Add(Path.Combine(localAppData, "Programs", "nodejs", cmdName));
        }
        else if (string.Equals(command, "winget", StringComparison.OrdinalIgnoreCase))
        {
            pathsToCheck.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps", cmdName));
        }

        foreach (var path in pathsToCheck)
        {
            if (_fileExists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static void TryRefreshProcessPath()
    {
        try
        {
            var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";

            var allPaths = new List<string>();
            allPaths.AddRange(currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            allPaths.AddRange(machinePath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            allPaths.AddRange(userPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            var merged = string.Join(
                Path.PathSeparator,
                allPaths.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists)
            );

            Environment.SetEnvironmentVariable("PATH", merged);
        }
        catch
        {
            // Ignore env refresh failures
        }
    }

    private static bool IsOutdated(string? actual, string? minimum)
    {
        if (string.IsNullOrWhiteSpace(minimum) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        return VersionParsing.CompareLoose(actual, minimum) < 0;
    }
}
