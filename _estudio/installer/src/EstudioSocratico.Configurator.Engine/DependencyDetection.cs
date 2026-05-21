using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class DependencyDetector(
    ICommandRunner runner,
    Func<VSCodePaths>? locateVSCode = null,
    string? managedToolsDirectory = null)
{
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
            DependencyId.Python => await DetectPythonAsync(requirement, cancellationToken).ConfigureAwait(false),
            _ => await DetectCommandAsync(requirement, ["--version"], null, cancellationToken).ConfigureAwait(false)
        };
    }

    private DependencyState DetectMsys2()
    {
        var bash = Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "bash.exe");
        var pacman = Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "pacman.exe");
        if (File.Exists(bash) && File.Exists(pacman))
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

    private async Task<DependencyState> DetectPythonAsync(DependencyRequirement requirement, CancellationToken cancellationToken)
    {
        var candidates = await ResolvePythonCandidatesAsync(cancellationToken).ConfigureAwait(false);
        InstallerError? lastError = null;

        foreach (var candidate in candidates)
        {
            var result = await runner.RunAsync(new CommandSpec
            {
                FileName = candidate,
                Arguments = ["--version"],
                Timeout = TimeSpan.FromSeconds(30),
                AllowNonZeroExitCode = true
            }, cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                var text = result.StandardOutput + Environment.NewLine + result.StandardError;
                var version = VersionParsing.FirstVersionLikeValue(text);
                var status = IsOutdated(version, requirement.MinimumVersion) ? DependencyStatus.Outdated : DependencyStatus.Ready;
                return new DependencyState
                {
                    Id = requirement.Id,
                    DisplayName = requirement.DisplayName,
                    Status = status,
                    Path = candidate,
                    Version = version,
                    Source = "command",
                    Recommendation = status == DependencyStatus.Outdated
                        ? $"Actualizar {requirement.DisplayName} a {requirement.MinimumVersion} o superior."
                        : null
                };
            }

            if (IsWindowsAppsPythonAlias(candidate, result))
            {
                continue;
            }

            lastError = new InstallerError
            {
                Code = InstallerErrorCode.COMMAND_FAILED,
                Title = $"{requirement.DisplayName} no responde",
                Description = result.StandardError,
                ProbableCause = "La instalacion existe pero el ejecutable fallo.",
                RecommendedAction = "Ejecuta Reparar para reinstalar o corregir PATH."
            };
        }

        return new DependencyState
        {
            Id = requirement.Id,
            DisplayName = requirement.DisplayName,
            Status = lastError is null ? DependencyStatus.Missing : DependencyStatus.Broken,
            Recommendation = lastError is null ? $"Instalar o reparar {requirement.DisplayName}." : null,
            Error = lastError
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
            if (File.Exists(exe))
            {
                return exe;
            }
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
            .FirstOrDefault(File.Exists);
    }

    private async Task<IReadOnlyList<string>> ResolvePythonCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        var resolved = await ResolveCommandPathListAsync("python", null, cancellationToken).ConfigureAwait(false);
        candidates.AddRange(resolved);

        foreach (var root in GetPythonInstallRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var direct in Directory.EnumerateFiles(root, "python.exe", SearchOption.TopDirectoryOnly))
            {
                candidates.Add(direct);
            }

            foreach (var directory in Directory.EnumerateDirectories(root, "Python*", SearchOption.TopDirectoryOnly))
            {
                var python = Path.Combine(directory, "python.exe");
                if (File.Exists(python))
                {
                    candidates.Add(python);
                }
            }
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path.Contains($"{Path.DirectorySeparatorChar}WindowsApps{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> ResolveCommandPathListAsync(string command, string? preferredDirectory, CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredDirectory))
        {
            var exe = Path.Combine(preferredDirectory, command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? command : command + ".exe");
            if (File.Exists(exe))
            {
                paths.Add(exe);
            }
        }

        var where = await runner.RunAsync(new CommandSpec
        {
            FileName = "where.exe",
            Arguments = [command],
            Timeout = TimeSpan.FromSeconds(10),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (where.Succeeded)
        {
            paths.AddRange(where.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(File.Exists));
        }

        return paths;
    }

    private static IEnumerable<string> GetPythonInstallRoots()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Python");
    }

    private static bool IsWindowsAppsPythonAlias(string candidate, CommandResult result)
    {
        if (!candidate.Contains($"{Path.DirectorySeparatorChar}WindowsApps{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return result.StandardError.Contains("no se encontró Python", StringComparison.OrdinalIgnoreCase) ||
               result.StandardError.Contains("no se encontro Python", StringComparison.OrdinalIgnoreCase) ||
               result.StandardError.Contains("Microsoft Store", StringComparison.OrdinalIgnoreCase);
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
