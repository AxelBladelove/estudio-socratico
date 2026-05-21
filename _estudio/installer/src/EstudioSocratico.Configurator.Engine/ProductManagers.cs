using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class GitHubAccountManager(ICommandRunner runner, ManifestManager manifestManager, LogManager logManager)
{
    private const string Host = "github.com";
    private static readonly HashSet<string> BootstrapWorkspaceEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ".estudio_usuario",
        ".gitignore",
        "usuario",
        "logs"
    };

    private static readonly HashSet<string> BootstrapUserEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "config",
        "errores.md",
        "exercism",
        "logs"
    };

    public async Task<AccountState> EnsureLoginAsync(bool switchAccount, CancellationToken cancellationToken)
    {
        if (switchAccount)
        {
            await runner.RunAsync(new CommandSpec
            {
                FileName = "gh",
                Arguments = ["auth", "logout", "--hostname", Host, "--yes"],
                AllowNonZeroExitCode = true,
                Timeout = TimeSpan.FromMinutes(2)
            }, cancellationToken).ConfigureAwait(false);
        }

        var status = await runner.RunAsync(new CommandSpec
        {
            FileName = "gh",
            Arguments = ["auth", "status", "--hostname", Host],
            AllowNonZeroExitCode = true,
            Timeout = TimeSpan.FromSeconds(30)
        }, cancellationToken).ConfigureAwait(false);

        if (!status.Succeeded)
        {
            var login = await runner.RunAsync(new CommandSpec
            {
                FileName = "gh",
                Arguments = ["auth", "login", "--hostname", Host, "--web", "--git-protocol", "https"],
                Timeout = TimeSpan.FromMinutes(10),
                AllowNonZeroExitCode = true
            }, cancellationToken).ConfigureAwait(false);

            if (!login.Succeeded)
            {
                throw new InvalidOperationException("GitHub CLI no pudo completar gh auth login --web.");
            }
        }

        await runner.RunAsync(new CommandSpec
        {
            FileName = "gh",
            Arguments = ["auth", "setup-git", "--hostname", Host],
            Timeout = TimeSpan.FromMinutes(2),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        var user = await runner.RunAsync(new CommandSpec
        {
            FileName = "gh",
            Arguments = ["api", "user", "--jq", ".login"],
            Timeout = TimeSpan.FromSeconds(30),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!user.Succeeded || string.IsNullOrWhiteSpace(user.StandardOutput))
        {
            throw new InvalidOperationException("No se pudo obtener el usuario autenticado de GitHub.");
        }

        var state = new AccountState
        {
            Configured = true,
            UserName = user.StandardOutput.Trim(),
            Host = Host,
            ValidatedAtUtc = DateTimeOffset.UtcNow,
            StorageWarning = status.StandardError.Contains("insecure", StringComparison.OrdinalIgnoreCase) ||
                             status.StandardOutput.Contains("insecure", StringComparison.OrdinalIgnoreCase)
                ? "GitHub CLI reporto almacenamiento inseguro de credenciales."
                : null
        };

        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        await manifestManager.SaveAsync(manifest with { GitHub = state }, cancellationToken).ConfigureAwait(false);
        return state;
    }

    public async Task ConfigureRepositoryAsync(string repoRoot, string localAlias, CancellationToken cancellationToken)
    {
        var account = await EnsureLoginAsync(switchAccount: false, cancellationToken).ConfigureAwait(false);
        var githubUser = account.UserName ?? localAlias;
        var ownsUpstream = string.Equals(githubUser, ProductInfo.BaseRepositoryOwner, StringComparison.OrdinalIgnoreCase);
        var originOwner = ownsUpstream ? ProductInfo.BaseRepositoryOwner : githubUser;
        var baseRepo = ProductInfo.BaseRepository;
        var workspaceRepo = $"{originOwner}/{ProductInfo.RepositoryName}";

        if (!ownsUpstream)
        {
            var forkView = await runner.RunAsync(new CommandSpec
            {
                FileName = "gh",
                Arguments = ["repo", "view", $"{githubUser}/{ProductInfo.RepositoryName}", "--json", "nameWithOwner", "--jq", ".nameWithOwner"],
                WorkingDirectory = repoRoot,
                Timeout = TimeSpan.FromSeconds(45),
                AllowNonZeroExitCode = true
            }, cancellationToken).ConfigureAwait(false);

            if (!forkView.Succeeded)
            {
                var fork = await runner.RunAsync(new CommandSpec
                {
                    FileName = "gh",
                    Arguments = ["repo", "fork", baseRepo, "--clone=false"],
                    WorkingDirectory = repoRoot,
                    Timeout = TimeSpan.FromMinutes(4),
                    AllowNonZeroExitCode = true
                }, cancellationToken).ConfigureAwait(false);

                if (!fork.Succeeded)
                {
                    throw new InvalidOperationException("No se pudo crear o validar el fork de GitHub.");
                }
            }
        }
        else
        {
            await logManager.WriteAsync("info", "github", $"La cuenta activa {githubUser} es dueña del repo original; no se crea fork.", cancellationToken)
                .ConfigureAwait(false);
        }

        await GitAsync(repoRoot, ["config", "--local", "github.user", githubUser], cancellationToken).ConfigureAwait(false);
        await GitAsync(repoRoot, ["config", "--local", "user.name", localAlias], cancellationToken).ConfigureAwait(false);
        await GitAsync(repoRoot, ["config", "--local", "user.email", $"{githubUser}@users.noreply.github.com"], cancellationToken).ConfigureAwait(false);
        await GitAsync(repoRoot, ["remote", "remove", "origin"], cancellationToken, allowFail: true).ConfigureAwait(false);
        await GitAsync(repoRoot, ["remote", "add", "origin", $"https://github.com/{workspaceRepo}.git"], cancellationToken).ConfigureAwait(false);
        await GitAsync(repoRoot, ["remote", "remove", "upstream"], cancellationToken, allowFail: true).ConfigureAwait(false);
        if (!ownsUpstream)
        {
            await GitAsync(repoRoot, ["remote", "add", "upstream", $"https://github.com/{baseRepo}.git"], cancellationToken).ConfigureAwait(false);
        }

        await logManager.WriteAsync("info", "github", $"Repositorio base {baseRepo}; workspace repo {workspaceRepo}.", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string> EnsureWorkspaceRepositoryAsync(
        string targetPath,
        string localAlias,
        bool skipGitHub,
        CancellationToken cancellationToken)
    {
        if (File.Exists(Path.Combine(targetPath, "AGENTS.md")))
        {
            if (!skipGitHub)
            {
                await ConfigureRepositoryAsync(targetPath, localAlias, cancellationToken).ConfigureAwait(false);
            }

            return targetPath;
        }

        if (Directory.Exists(targetPath) && Directory.EnumerateFileSystemEntries(targetPath).Any())
        {
            if (LooksLikeBootstrapWorkspace(targetPath))
            {
                await logManager.WriteAsync("info", "workspace", $"Recuperando bootstrap parcial en {targetPath}.", cancellationToken)
                    .ConfigureAwait(false);
                await CompleteBootstrapWorkspaceAsync(targetPath, localAlias, skipGitHub, cancellationToken).ConfigureAwait(false);
                if (!skipGitHub)
                {
                    await ConfigureRepositoryAsync(targetPath, localAlias, cancellationToken).ConfigureAwait(false);
                }

                return targetPath;
            }

            throw new InvalidOperationException("La carpeta de workspace ya existe y no parece ser Estudio Socratico.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await CloneWorkspaceAsync(targetPath, localAlias, skipGitHub, cancellationToken).ConfigureAwait(false);
        if (!skipGitHub)
        {
            await ConfigureRepositoryAsync(targetPath, localAlias, cancellationToken).ConfigureAwait(false);
        }
        return targetPath;
    }

    private async Task CloneWorkspaceAsync(string targetPath, string localAlias, bool skipGitHub, CancellationToken cancellationToken)
    {
        if (!skipGitHub)
        {
            var account = await EnsureLoginAsync(switchAccount: false, cancellationToken).ConfigureAwait(false);
            var githubUser = account.UserName ?? localAlias;
            var baseRepo = ProductInfo.BaseRepository;
            if (!string.Equals(githubUser, ProductInfo.BaseRepositoryOwner, StringComparison.OrdinalIgnoreCase))
            {
                var fork = await runner.RunAsync(new CommandSpec
                {
                    FileName = "gh",
                    Arguments = ["repo", "fork", baseRepo, "--clone=false"],
                    Timeout = TimeSpan.FromMinutes(4),
                    AllowNonZeroExitCode = true
                }, cancellationToken).ConfigureAwait(false);

                if (!fork.Succeeded)
                {
                    throw new InvalidOperationException("No se pudo crear el fork de GitHub para clonar el workspace.");
                }
            }

            var workspaceRepoOwner = string.Equals(githubUser, ProductInfo.BaseRepositoryOwner, StringComparison.OrdinalIgnoreCase)
                ? ProductInfo.BaseRepositoryOwner
                : githubUser;
            await GitAsync(Directory.GetParent(targetPath)!.FullName, ["clone", $"https://github.com/{workspaceRepoOwner}/{ProductInfo.RepositoryName}.git", targetPath], cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await GitAsync(Directory.GetParent(targetPath)!.FullName, ["clone", $"https://github.com/{ProductInfo.BaseRepository}.git", targetPath], cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task CompleteBootstrapWorkspaceAsync(string targetPath, string localAlias, bool skipGitHub, CancellationToken cancellationToken)
    {
        var backupPath = targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".bootstrap-" + Guid.NewGuid().ToString("N");
        Directory.Move(targetPath, backupPath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await CloneWorkspaceAsync(targetPath, localAlias, skipGitHub, cancellationToken).ConfigureAwait(false);
            MergeBootstrapWorkspace(backupPath, targetPath);
        }
        catch
        {
            try
            {
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup before restoring bootstrap workspace.
            }

            if (Directory.Exists(backupPath))
            {
                Directory.Move(backupPath, targetPath);
            }

            throw;
        }

        Directory.Delete(backupPath, recursive: true);
    }

    private static bool LooksLikeBootstrapWorkspace(string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            return false;
        }

        if (File.Exists(Path.Combine(targetPath, "AGENTS.md")) ||
            Directory.Exists(Path.Combine(targetPath, ".git")) ||
            Directory.Exists(Path.Combine(targetPath, "_estudio")) ||
            Directory.Exists(Path.Combine(targetPath, "Ejercicios")))
        {
            return false;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(targetPath))
        {
            var name = Path.GetFileName(entry);
            if (!BootstrapWorkspaceEntries.Contains(name))
            {
                return false;
            }

            if (string.Equals(name, "usuario", StringComparison.OrdinalIgnoreCase) && !LooksLikeBootstrapUserDirectory(entry))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeBootstrapUserDirectory(string userPath)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(userPath))
        {
            var name = Path.GetFileName(entry);
            if (!BootstrapUserEntries.Contains(name))
            {
                return false;
            }
        }

        return true;
    }

    private static void MergeBootstrapWorkspace(string source, string destination)
    {
        MergeDirectoryIfExists(Path.Combine(source, "usuario"), Path.Combine(destination, "usuario"));
        MergeDirectoryIfExists(Path.Combine(source, "logs"), Path.Combine(destination, "logs"));

        var aliasPath = Path.Combine(source, ".estudio_usuario");
        if (File.Exists(aliasPath))
        {
            File.Copy(aliasPath, Path.Combine(destination, ".estudio_usuario"), overwrite: true);
        }

        MergeGitIgnoreIfExists(Path.Combine(source, ".gitignore"), Path.Combine(destination, ".gitignore"));
    }

    private static void MergeDirectoryIfExists(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void MergeGitIgnoreIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var lines = File.Exists(destinationPath)
            ? File.ReadAllLines(destinationPath).ToList()
            : [];
        foreach (var line in File.ReadAllLines(sourcePath))
        {
            if (!lines.Any(existing => string.Equals(existing.Trim(), line.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(line);
            }
        }

        File.WriteAllLines(destinationPath, lines);
    }

    private Task<CommandResult> GitAsync(string repoRoot, IReadOnlyList<string> args, CancellationToken cancellationToken, bool allowFail = false)
    {
        return runner.RunAsync(new CommandSpec
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = repoRoot,
            Timeout = TimeSpan.FromMinutes(2),
            AllowNonZeroExitCode = allowFail
        }, cancellationToken);
    }
}

public sealed class ExercismManager(ICommandRunner runner, ManifestManager manifestManager, AppPaths paths, LogManager logManager)
{
    public const string TokenUrl = "https://exercism.org/settings/api_cli";
    public const string CTrackUrl = "https://exercism.org/tracks/c";

    public async Task<AccountState> ConfigureTokenAsync(string token, string workspacePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
        {
            throw new InvalidOperationException("El token de Exercism esta vacio o no parece valido.");
        }

        Directory.CreateDirectory(Path.Combine(workspacePath, "usuario", "exercism"));
        var exercismWorkspace = Path.Combine(workspacePath, "usuario", "exercism");
        var configure = await runner.RunAsync(new CommandSpec
        {
            FileName = "exercism",
            Arguments = ["configure", "--token", token.Trim(), "--workspace", exercismWorkspace],
            Timeout = TimeSpan.FromMinutes(2),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!configure.Succeeded)
        {
            throw new InvalidOperationException("Exercism CLI rechazo el token o no pudo guardar la configuracion.");
        }

        await ValidateTokenAsync(cancellationToken).ConfigureAwait(false);

        var state = new AccountState
        {
            Configured = true,
            Host = "exercism.org",
            ValidatedAtUtc = DateTimeOffset.UtcNow
        };
        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        await manifestManager.SaveAsync(manifest with { Exercism = state }, cancellationToken).ConfigureAwait(false);
        await logManager.WriteAsync("info", "exercism", "Exercism CLI configurado sin registrar token.", cancellationToken)
            .ConfigureAwait(false);
        return state;
    }

    public async Task ValidateTokenAsync(CancellationToken cancellationToken)
    {
        // Step 1: Confirm CLI is operational
        var version = await runner.RunAsync(new CommandSpec
        {
            FileName = "exercism",
            Arguments = ["version"],
            Timeout = TimeSpan.FromSeconds(15),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!version.Succeeded)
        {
            throw new InvalidOperationException("Exercism CLI no responde a 'exercism version'.");
        }

        // Step 2: Confirm workspace is configured
        var workspace = await runner.RunAsync(new CommandSpec
        {
            FileName = "exercism",
            Arguments = ["workspace"],
            Timeout = TimeSpan.FromSeconds(15),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!workspace.Succeeded || string.IsNullOrWhiteSpace(workspace.StandardOutput))
        {
            throw new InvalidOperationException("Exercism CLI no tiene un workspace configurado.");
        }

        // Step 3: Smoke download in temp directory with --force
        var temp = Path.Combine(paths.LocalAppDataRoot, "Diagnostics", "exercism-token-check");
        if (Directory.Exists(temp))
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
        Directory.CreateDirectory(temp);

        var configureTemp = await runner.RunAsync(new CommandSpec
        {
            FileName = "exercism",
            Arguments = ["configure", "--workspace", temp],
            Timeout = TimeSpan.FromSeconds(30),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        var result = await runner.RunAsync(new CommandSpec
        {
            FileName = "exercism",
            Arguments = ["download", "--track=c", "--exercise=hello-world", "--force"],
            WorkingDirectory = temp,
            Timeout = TimeSpan.FromMinutes(3),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        // Restore real workspace
        var realWorkspace = workspace.StandardOutput.Trim();
        await runner.RunAsync(new CommandSpec
        {
            FileName = "exercism",
            Arguments = ["configure", "--workspace", realWorkspace],
            Timeout = TimeSpan.FromSeconds(30),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        try
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
        catch
        {
            // Non-critical cleanup
        }

        if (result.Succeeded) return;

        // Classify the failure
        var combined = $"{result.StandardOutput}\n{result.StandardError}";
        if (combined.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // Exercise already downloaded — token is valid
            await logManager.WriteAsync("info", "exercism", "hello-world ya existia; token considerado valido.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (combined.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("403", StringComparison.Ordinal) ||
            combined.Contains("401", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("El token de Exercism fue rechazado por la API (no autorizado).");
        }

        if (combined.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Error de red al validar Exercism. Verifica tu conexion a internet.");
        }

        if (combined.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("access denied", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Error de permisos al descargar ejercicio de Exercism.");
        }

        throw new InvalidOperationException($"Exercism CLI devolvio un error inesperado al validar: {combined.Trim()}");
    }
}

public sealed record VSCodePaths(string? CodeExe, string? CodeCmd)
{
    public bool HasCodeExe => !string.IsNullOrWhiteSpace(CodeExe) && File.Exists(CodeExe);
    public bool HasCodeCmd => !string.IsNullOrWhiteSpace(CodeCmd) && File.Exists(CodeCmd);
}

public static class VSCodeLocator
{
    public static VSCodePaths Resolve()
    {
        var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = new[]
        {
            Path.Combine(userRoot, "Programs", "Microsoft VS Code"),
            Path.Combine(programFiles, "Microsoft VS Code"),
            Path.Combine(programFilesX86, "Microsoft VS Code")
        };

        return ResolveFromInstallRoots(candidates);
    }

    public static VSCodePaths ResolveFromInstallRoots(IEnumerable<string> installRoots)
    {
        var candidates = installRoots.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        var codeExe = candidates.Select(path => Path.Combine(path, "Code.exe")).FirstOrDefault(File.Exists);
        var codeCmd = candidates.Select(path => Path.Combine(path, "bin", "code.cmd")).FirstOrDefault(File.Exists);
        return new VSCodePaths(codeExe, codeCmd);
    }

    public static CommandSpec BuildCodeCmdCommand(string codeCmd, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout)
    {
        var command = $"/c \"{codeCmd}\" {string.Join(" ", arguments.Select(QuoteForCmd))}";
        return new CommandSpec
        {
            FileName = "cmd.exe",
            ArgumentString = command,
            WorkingDirectory = workingDirectory,
            Timeout = timeout,
            AllowNonZeroExitCode = true
        };
    }

    private static string QuoteForCmd(string value)
    {
        return value.Any(char.IsWhiteSpace) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}

public sealed class ExtensionManager(AppPaths paths, LogManager logManager, string? userProfileRoot = null)
{
    private sealed record LocalExtensionDescriptor(
        string Id,
        string Publisher,
        string Name,
        string Version,
        string SourcePath,
        bool ActivityBarConfigured,
        bool CommandsRegistered,
        bool ExercisePanelConfigured,
        bool ManagerScriptExists);

    public string GetSourcePath(string workspacePath) =>
        Path.Combine(workspacePath, "_estudio", "soporte", "vscode", "estudio-exercism");

    public string GetManagerScriptPath(string workspacePath) =>
        Path.Combine(workspacePath, "_estudio", "soporte", "exercism", "manager.ps1");

    public async Task<string> GetExtensionIdAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var descriptor = await DescribeAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        return descriptor.Id;
    }

    public string? FindInstalledExtensionPath(string extensionId)
    {
        var extensionsRoot = GetInstalledExtensionsRoot();

        if (!Directory.Exists(extensionsRoot))
        {
            return null;
        }

        return Directory.EnumerateDirectories(extensionsRoot, $"{extensionId}-*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public async Task InstallLocalExtensionAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var descriptor = await DescribeAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        var destination = Path.Combine(
            GetInstalledExtensionsRoot(),
            $"{descriptor.Id}-{descriptor.Version}");

        var installedRoot = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(installedRoot);
        foreach (var existing in Directory.EnumerateDirectories(installedRoot, $"{descriptor.Id}-*", SearchOption.TopDirectoryOnly))
        {
            Directory.Delete(existing, recursive: true);
        }

        CopyDirectory(descriptor.SourcePath, destination);
        await logManager.WriteAsync("info", "vscode-extension", $"Extension local instalada en perfil VS Code: {descriptor.Id}", cancellationToken)
            .ConfigureAwait(false);

        var manifest = await new ManifestManager(paths).LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!manifest.VSCodeExtensionsInstalled.Contains(descriptor.Id, StringComparer.OrdinalIgnoreCase))
        {
            manifest.VSCodeExtensionsInstalled.Add(descriptor.Id);
        }
        await new ManifestManager(paths).SaveAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VSCodeExtensionState> DescribeStateAsync(
        string workspacePath,
        bool installedInVSCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
        {
            return new VSCodeExtensionState
            {
                Status = ResourceStatus.NeedsUserAction,
                HumanStatus = GlobalStateCalculator.GetHumanStatus(ResourceStatus.NeedsUserAction),
                HumanDescription = "La extension se instalará cuando exista el workspace.",
                InstalledInVSCode = installedInVSCode
            };
        }

        try
        {
            var descriptor = await DescribeAsync(workspacePath, cancellationToken).ConfigureAwait(false);
            var installedPath = FindInstalledExtensionPath(descriptor.Id);
            var ready = installedInVSCode &&
                        descriptor.ActivityBarConfigured &&
                        descriptor.CommandsRegistered &&
                        descriptor.ExercisePanelConfigured &&
                        descriptor.ManagerScriptExists;
            var status = ready ? ResourceStatus.Ready : ResourceStatus.Broken;
            var issues = new List<string>();
            if (!installedInVSCode)
            {
                issues.Add("VS Code no reporta la extension instalada.");
            }

            if (!descriptor.ActivityBarConfigured)
            {
                issues.Add("No expone Estudio/Ejercicios en la activity bar.");
            }

            if (!descriptor.CommandsRegistered)
            {
                issues.Add("No registra los comandos esperados.");
            }

            if (!descriptor.ExercisePanelConfigured)
            {
                issues.Add("No expone el panel de ejercicios.");
            }

            if (!descriptor.ManagerScriptExists)
            {
                issues.Add("Falta manager.ps1.");
            }

            return new VSCodeExtensionState
            {
                ExtensionId = descriptor.Id,
                Status = status,
                HumanStatus = GlobalStateCalculator.GetHumanStatus(status),
                HumanDescription = ready
                    ? "Instalada, visible como Estudio/Ejercicios y lista para abrir el panel."
                    : string.Join(" ", issues),
                SourcePath = descriptor.SourcePath,
                InstalledPath = installedPath,
                SourceExists = true,
                InstalledInVSCode = installedInVSCode,
                ActivityBarConfigured = descriptor.ActivityBarConfigured,
                CommandsRegistered = descriptor.CommandsRegistered,
                ExercisePanelAvailable = descriptor.ExercisePanelConfigured,
                ManagerScriptExists = descriptor.ManagerScriptExists
            };
        }
        catch (DirectoryNotFoundException ex)
        {
            return new VSCodeExtensionState
            {
                Status = ResourceStatus.Broken,
                HumanStatus = GlobalStateCalculator.GetHumanStatus(ResourceStatus.Broken),
                HumanDescription = ex.Message,
                SourceExists = false,
                InstalledInVSCode = installedInVSCode
            };
        }
    }

    private async Task<LocalExtensionDescriptor> DescribeAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var source = GetSourcePath(workspacePath);
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException("No se encontro la extension local de VS Code.");
        }

        var packageJsonPath = Path.Combine(source, "package.json");
        var packageJson = JsonNode.Parse(await File.ReadAllTextAsync(packageJsonPath, cancellationToken).ConfigureAwait(false))!
            .AsObject();
        var name = packageJson["name"]?.GetValue<string>() ?? "estudio-exercism";
        var publisher = packageJson["publisher"]?.GetValue<string>() ?? "estudio-socratico";
        var version = packageJson["version"]?.GetValue<string>() ?? "0.0.0";
        var commands = packageJson["contributes"]?["commands"]?.AsArray();
        var activityBar = packageJson["contributes"]?["viewsContainers"]?["activitybar"]?.AsArray();
        var views = packageJson["contributes"]?["views"]?["estudioSocratico"]?.AsArray();
        var commandIds = commands?
            .Select(node => node?["command"]?.GetValue<string>() ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var activityBarConfigured = activityBar?.Any(node =>
            string.Equals(node?["title"]?.GetValue<string>(), "Estudio", StringComparison.OrdinalIgnoreCase)) == true;
        var exercisePanelConfigured = views?.Any(node =>
            string.Equals(node?["id"]?.GetValue<string>(), "estudioExercism.view", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node?["name"]?.GetValue<string>(), "Ejercicios", StringComparison.OrdinalIgnoreCase)) == true;
        var commandsRegistered =
            commandIds.Contains("estudioExercism.openPanel") &&
            commandIds.Contains("estudioExercism.openApiKeyConfig") &&
            commandIds.Contains("estudioExercism.revealApiKeyConfig");

        return new LocalExtensionDescriptor(
            Id: $"{publisher}.{name}",
            Publisher: publisher,
            Name: name,
            Version: version,
            SourcePath: source,
            ActivityBarConfigured: activityBarConfigured,
            CommandsRegistered: commandsRegistered,
            ExercisePanelConfigured: exercisePanelConfigured,
            ManagerScriptExists: File.Exists(GetManagerScriptPath(workspacePath)));
    }

    private string GetInstalledExtensionsRoot()
    {
        var profileRoot = userProfileRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profileRoot, ".vscode", "extensions");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (directory.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = file.Replace(source, destination, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}

public sealed class VSCodeManager(
    ICommandRunner runner,
    ExtensionManager extensionManager,
    ManifestManager manifestManager,
    LogManager logManager,
    Func<VSCodePaths>? locateVSCode = null)
{
    public async Task<VSCodeExtensionState> DiagnoseExtensionAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var paths = ResolveVSCode();
        var installedInVSCode = false;
        if (paths.HasCodeCmd)
        {
            var listed = await runner.RunAsync(VSCodeLocator.BuildCodeCmdCommand(
                paths.CodeCmd!,
                ["--list-extensions", "--show-versions"],
                string.IsNullOrWhiteSpace(workspacePath) ? Environment.CurrentDirectory : workspacePath,
                TimeSpan.FromSeconds(45)), cancellationToken).ConfigureAwait(false);

            installedInVSCode = listed.Succeeded &&
                listed.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(line => line.StartsWith(ProductInfo.VSCodeExtensionId, StringComparison.OrdinalIgnoreCase));
        }

        return await extensionManager.DescribeStateAsync(workspacePath, installedInVSCode, cancellationToken).ConfigureAwait(false);
    }

    public async Task PrepareAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var paths = ResolveVSCode();
        if (!paths.HasCodeExe)
        {
            throw new InvalidOperationException("VS Code no esta instalado o Code.exe no se encontro en una ruta soportada.");
        }

        if (!paths.HasCodeCmd)
        {
            throw new InvalidOperationException("VS Code esta instalado, pero code.cmd no se encontro para instalar extensiones.");
        }

        var version = await runner.RunAsync(VSCodeLocator.BuildCodeCmdCommand(
            paths.CodeCmd!,
            ["--version"],
            workspacePath,
            TimeSpan.FromSeconds(30)), cancellationToken).ConfigureAwait(false);
        if (!version.Succeeded)
        {
            throw new InvalidOperationException("VS Code no respondio correctamente a code.cmd --version.");
        }

        var cpptools = await runner.RunAsync(VSCodeLocator.BuildCodeCmdCommand(
            paths.CodeCmd!,
            ["--install-extension", "ms-vscode.cpptools", "--force"],
            workspacePath,
            TimeSpan.FromMinutes(4)), cancellationToken).ConfigureAwait(false);
        if (!cpptools.Succeeded)
        {
            throw new InvalidOperationException("VS Code no pudo instalar la extension ms-vscode.cpptools.");
        }

        await extensionManager.InstallLocalExtensionAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await ApplyWorkspaceSettingsAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await logManager.WriteAsync("info", "vscode", "VS Code preparado con C/C++, F9 y extension local.", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RepairLocalExtensionAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var paths = ResolveVSCode();
        if (!paths.HasCodeCmd)
        {
            throw new InvalidOperationException("VS Code esta instalado, pero code.cmd no se encontro para reinstalar la extension.");
        }

        await extensionManager.InstallLocalExtensionAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await logManager.WriteAsync("info", "vscode-extension", "Extension local de VS Code reinstalada.", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task OpenWorkspaceAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var paths = ResolveVSCode();
        if (!paths.HasCodeExe)
        {
            throw new InvalidOperationException("VS Code no esta instalado o Code.exe no se encontro en una ruta soportada.");
        }

        var result = await runner.RunAsync(new CommandSpec
        {
            FileName = paths.CodeExe!,
            Arguments = [workspacePath],
            WorkingDirectory = workspacePath,
            Timeout = TimeSpan.FromSeconds(20),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("No se pudo abrir el workspace en VS Code.");
        }
    }

    public async Task OpenExercisePanelAsync(string workspacePath, CancellationToken cancellationToken)
    {
        await OpenWorkspaceAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        Process.Start(new ProcessStartInfo($"vscode://{ProductInfo.VSCodeExtensionId}/openPanel")
        {
            UseShellExecute = true
        });
    }

    private VSCodePaths ResolveVSCode() => locateVSCode?.Invoke() ?? VSCodeLocator.Resolve();

    private async Task ApplyWorkspaceSettingsAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var vscodeDir = Path.Combine(workspacePath, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        var settingsPath = Path.Combine(vscodeDir, "settings.json");
        JsonObject settings;
        if (File.Exists(settingsPath))
        {
            settings = JsonNode.Parse(await File.ReadAllTextAsync(settingsPath, cancellationToken).ConfigureAwait(false)) as JsonObject ?? new JsonObject();
        }
        else
        {
            settings = new JsonObject();
        }

        settings["C_Cpp.default.compilerPath"] = "C:/msys64/ucrt64/bin/gcc.exe";
        settings["terminal.integrated.defaultProfile.windows"] = "PowerShell";
        settings["security.workspace.trust.untrustedFiles"] = "open";
        settings["C_Cpp.default.includePath"] = new JsonArray(
            JsonValue.Create("${workspaceFolder}/_estudio/include"),
            JsonValue.Create("${default}"));
        await File.WriteAllTextAsync(settingsPath, settings.ToJsonString(JsonDefaults.Options), cancellationToken)
            .ConfigureAwait(false);

        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        manifest.VSCodeSettingsApplied.Add(".vscode/settings.json");
        await manifestManager.SaveAsync(manifest, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class WorkspaceManager(AppPaths paths, ManifestManager manifestManager, LogManager logManager)
{
    private const string ExtensionConfigRelativePath = "usuario\\config\\estudio-socratico.extension.local.json";
    private const string ExtensionConfigExampleRelativePath = "usuario\\config\\estudio-socratico.extension.example.json";

    public static string DefaultExtensionConfigJson { get; } =
"""
{
  "apiKey": "",
  "provider": "gemini",
  "features": {
    "translateIntroductions": true,
    "importExercism": true,
    "importAlejandroGists": true
  }
}
""";

    public async Task<string> PrepareAsync(string? requestedPath, string localAlias, CancellationToken cancellationToken)
    {
        var normalizedAlias = LocalAliasNormalizer.Normalize(localAlias);
        var workspacePath = requestedPath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            workspacePath = paths.GetRecommendedWorkspacePath(normalizedAlias);
        }

        Directory.CreateDirectory(workspacePath);
        RequireWorkspaceShape(workspacePath);

        await File.WriteAllTextAsync(Path.Combine(workspacePath, ".estudio_usuario"), normalizedAlias, cancellationToken)
            .ConfigureAwait(false);
        var userDir = Path.Combine(workspacePath, "usuario");
        Directory.CreateDirectory(userDir);
        var errors = Path.Combine(userDir, "errores.md");
        if (!File.Exists(errors))
        {
            var template = Path.Combine(workspacePath, "_estudio", "errores.template.md");
            if (File.Exists(template))
            {
                File.Copy(template, errors);
            }
            else
            {
                await File.WriteAllTextAsync(errors, "<!-- Archivo de errores inicializado automaticamente. -->", cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        Directory.CreateDirectory(Path.Combine(userDir, "logs"));
        Directory.CreateDirectory(Path.Combine(userDir, "config"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "logs"));
        await EnsureExtensionApiKeyConfigAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        await manifestManager.SaveAsync(manifest with
        {
            WorkspacePath = workspacePath,
            LocalAlias = normalizedAlias,
            BuildFlowValidated = false,
            BuildFlowValidatedAtUtc = null
        }, cancellationToken).ConfigureAwait(false);
        await logManager.WriteAsync("info", "workspace", $"Workspace preparado: {workspacePath}", cancellationToken)
            .ConfigureAwait(false);
        return workspacePath;
    }

    public string GetRecommendedWorkspacePath(string localAlias) =>
        paths.GetRecommendedWorkspacePath(localAlias);

    public ExtensionApiKeyConfigState DescribeExtensionApiKeyConfig(string workspacePath)
    {
        var localConfigPath = Path.Combine(workspacePath, ExtensionConfigRelativePath);
        var exampleConfigPath = Path.Combine(workspacePath, ExtensionConfigExampleRelativePath);
        var localExists = File.Exists(localConfigPath);
        var exampleExists = File.Exists(exampleConfigPath);
        var ready = localExists && exampleExists;
        var status = ready ? ResourceStatus.Ready : ResourceStatus.NeedsUserAction;

        return new ExtensionApiKeyConfigState
        {
            Status = status,
            HumanStatus = GlobalStateCalculator.GetHumanStatus(status),
            HumanDescription = ready
                ? "Archivo local listo para pegar la API key manualmente."
                : "Se creará dentro de usuario/config cuando el workspace quede preparado.",
            LocalConfigPath = localConfigPath,
            ExampleConfigPath = exampleConfigPath,
            LocalConfigExists = localExists,
            ExampleConfigExists = exampleExists
        };
    }

    public async Task<ExtensionApiKeyConfigState> EnsureExtensionApiKeyConfigAsync(string workspacePath, CancellationToken cancellationToken)
    {
        RequireWorkspaceShape(workspacePath);
        var localConfigPath = Path.Combine(workspacePath, ExtensionConfigRelativePath);
        var exampleConfigPath = Path.Combine(workspacePath, ExtensionConfigExampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(localConfigPath)!);

        if (!File.Exists(exampleConfigPath))
        {
            await File.WriteAllTextAsync(exampleConfigPath, DefaultExtensionConfigJson, cancellationToken).ConfigureAwait(false);
        }

        if (!File.Exists(localConfigPath))
        {
            await File.WriteAllTextAsync(localConfigPath, DefaultExtensionConfigJson, cancellationToken).ConfigureAwait(false);
        }

        await EnsureGitIgnoreEntryAsync(workspacePath, ExtensionConfigRelativePath.Replace('\\', '/'), cancellationToken).ConfigureAwait(false);
        return DescribeExtensionApiKeyConfig(workspacePath);
    }

    public void RequireWorkspaceShape(string workspacePath)
    {
        var required = new[]
        {
            "AGENTS.md",
            Path.Combine("_estudio", "soporte", "scripts", "build.cmd"),
            Path.Combine("_estudio", "soporte", "scripts", "compilar_y_grabar.bat"),
            Path.Combine("_estudio", "include", "conio.h"),
            Path.Combine("_estudio", "soporte", "exercism", "manager.ps1")
        };

        foreach (var item in required)
        {
            if (!File.Exists(Path.Combine(workspacePath, item)))
            {
                throw new InvalidOperationException($"Workspace incompleto: falta {item}.");
            }
        }
    }

    private async Task EnsureGitIgnoreEntryAsync(string workspacePath, string relativePath, CancellationToken cancellationToken)
    {
        var gitignorePath = Path.Combine(workspacePath, ".gitignore");
        var normalizedEntry = relativePath.Replace('\\', '/');
        var lines = File.Exists(gitignorePath)
            ? (await File.ReadAllLinesAsync(gitignorePath, cancellationToken).ConfigureAwait(false)).ToList()
            : [];

        if (!lines.Any(line => string.Equals(line.Trim(), normalizedEntry, StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add(normalizedEntry);
            await File.WriteAllLinesAsync(gitignorePath, lines, cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class TelemetryCompatibilityManager(ICommandRunner runner, LogManager logManager)
{
    public async Task ValidateBuildFlowAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var runtime = Path.Combine(workspacePath, "_estudio", "soporte", "runtime", "installer-probe");
        Directory.CreateDirectory(runtime);
        var probe = Path.Combine(runtime, "probe.c");
        await File.WriteAllTextAsync(probe, "#include <stdio.h>\nint main(void){printf(\"ok\\n\");return 0;}\n", cancellationToken)
            .ConfigureAwait(false);

        var build = Path.Combine(workspacePath, "_estudio", "soporte", "scripts", "build.cmd");
        var result = await runner.RunAsync(new CommandSpec
        {
            FileName = build,
            Arguments = ["--inline", probe, "--installer-smoke", "--non-interactive"],
            WorkingDirectory = workspacePath,
            Environment = new Dictionary<string, string?>
            {
                ["ESTUDIO_INSTALLER_SMOKE"] = "1",
                ["ESTUDIO_NONINTERACTIVE"] = "1",
                ["ESTUDIO_SKIP_PAUSE"] = "1",
                ["ESTUDIO_SKIP_COMMIT"] = "1"
            },
            Timeout = TimeSpan.FromMinutes(2),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        var fallbackSuccess = IsInteractiveTailSuccess(result);
        if (!result.Succeeded && !fallbackSuccess)
        {
            throw new InvalidOperationException("El flujo real build.cmd/compilar_y_grabar.bat no valido correctamente.");
        }

        var smokeStatus = fallbackSuccess ? "passed-interactive-tail-detected" : "passed";
        await logManager.WriteAsync("info", "telemetry", $"Flujo compilar y grabar validado con probe. smokeTestStatus={smokeStatus}", cancellationToken)
            .ConfigureAwait(false);
    }

    public static bool IsInteractiveTailSuccess(CommandResult result)
    {
        if (!result.TimedOut)
        {
            return false;
        }

        var output = $"{result.StandardOutput}\n{result.StandardError}";
        return output.Contains("[OK] Compilacion exitosa", StringComparison.OrdinalIgnoreCase) &&
               output.Contains("ok", StringComparison.OrdinalIgnoreCase) &&
               output.Contains("Process returned 0", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class GistImporterManager
{
    public void ValidateGistCatalogs(string workspacePath)
    {
        var alejandro = Path.Combine(workspacePath, "_estudio", "soporte", "exercism", "catalogs", "alejandro.json");
        if (!File.Exists(alejandro))
        {
            throw new InvalidOperationException("No se encontro el catalogo Alejandro para importacion por Gist.");
        }

        var text = File.ReadAllText(alejandro);
        if (!text.Contains("gist.github", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("raw.githubusercontent", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El catalogo Alejandro no contiene URLs de Gist/raw verificables.");
        }
    }
}
