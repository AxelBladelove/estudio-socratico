using System.Text.Json;
using System.Text.Json.Nodes;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class GitHubAccountManager(ICommandRunner runner, ManifestManager manifestManager, LogManager logManager)
{
    private const string Host = "github.com";
    private const string UpstreamRepository = "AxelBladelove/estudio-socratico";

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

        await runner.RunAsync(new CommandSpec
        {
            FileName = "gh",
            Arguments = ["repo", "fork", UpstreamRepository, "--remote=false"],
            WorkingDirectory = repoRoot,
            Timeout = TimeSpan.FromMinutes(4),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        await GitAsync(repoRoot, ["config", "--local", "github.user", githubUser], cancellationToken).ConfigureAwait(false);
        await GitAsync(repoRoot, ["config", "--local", "user.name", localAlias], cancellationToken).ConfigureAwait(false);
        await GitAsync(repoRoot, ["config", "--local", "user.email", $"{githubUser}@users.noreply.github.com"], cancellationToken).ConfigureAwait(false);
        await GitAsync(repoRoot, ["remote", "remove", "origin"], cancellationToken, allowFail: true).ConfigureAwait(false);
        await GitAsync(repoRoot, ["remote", "add", "origin", $"https://github.com/{githubUser}/estudio-socratico.git"], cancellationToken, allowFail: true).ConfigureAwait(false);
        await GitAsync(repoRoot, ["remote", "remove", "upstream"], cancellationToken, allowFail: true).ConfigureAwait(false);
        await GitAsync(repoRoot, ["remote", "add", "upstream", $"https://github.com/{UpstreamRepository}.git"], cancellationToken, allowFail: true).ConfigureAwait(false);
        await logManager.WriteAsync("info", "github", $"Repositorio configurado para {githubUser}.", cancellationToken)
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
            throw new InvalidOperationException("La carpeta de workspace ya existe y no parece ser Estudio Socratico.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (!skipGitHub)
        {
            var account = await EnsureLoginAsync(switchAccount: false, cancellationToken).ConfigureAwait(false);
            var githubUser = account.UserName ?? localAlias;
            await runner.RunAsync(new CommandSpec
            {
                FileName = "gh",
                Arguments = ["repo", "fork", UpstreamRepository, "--clone=false"],
                Timeout = TimeSpan.FromMinutes(4),
                AllowNonZeroExitCode = true
            }, cancellationToken).ConfigureAwait(false);
            await GitAsync(Directory.GetParent(targetPath)!.FullName, ["clone", $"https://github.com/{githubUser}/estudio-socratico.git", targetPath], cancellationToken)
                .ConfigureAwait(false);
            await ConfigureRepositoryAsync(targetPath, localAlias, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await GitAsync(Directory.GetParent(targetPath)!.FullName, ["clone", $"https://github.com/{UpstreamRepository}.git", targetPath], cancellationToken)
                .ConfigureAwait(false);
        }

        return targetPath;
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
        var temp = Path.Combine(paths.LocalAppDataRoot, "Diagnostics", "exercism-token-check");
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }
        Directory.CreateDirectory(temp);

        var result = await runner.RunAsync(new CommandSpec
        {
            FileName = "exercism",
            Arguments = ["download", "--track=c", "--exercise=hello-world", "--output-dir", temp],
            Timeout = TimeSpan.FromMinutes(3),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.Delete(temp, recursive: true);
        }
        catch
        {
            // Non-critical cleanup after validation.
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("No se pudo validar el token descargando hello-world del track C.");
        }
    }
}

public sealed class ExtensionManager(AppPaths paths, LogManager logManager)
{
    public async Task InstallLocalExtensionAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var source = Path.Combine(workspacePath, "_estudio", "soporte", "vscode", "estudio-exercism");
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException("No se encontro la extension local de VS Code.");
        }

        var packageJson = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(source, "package.json"), cancellationToken)
            .ConfigureAwait(false))!;
        var name = packageJson["name"]?.GetValue<string>() ?? "estudio-exercism";
        var publisher = packageJson["publisher"]?.GetValue<string>() ?? "estudio-socratico";
        var version = packageJson["version"]?.GetValue<string>() ?? "0.0.0";
        var destination = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".vscode",
            "extensions",
            $"{publisher}.{name}-{version}");

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        CopyDirectory(source, destination);
        await logManager.WriteAsync("info", "vscode-extension", $"Extension local instalada en perfil VS Code: {publisher}.{name}", cancellationToken)
            .ConfigureAwait(false);

        var manifest = await new ManifestManager(paths).LoadAsync(cancellationToken).ConfigureAwait(false);
        manifest.VSCodeExtensionsInstalled.Add($"{publisher}.{name}");
        await new ManifestManager(paths).SaveAsync(manifest, cancellationToken).ConfigureAwait(false);
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

public sealed class VSCodeManager(ICommandRunner runner, ExtensionManager extensionManager, ManifestManager manifestManager, LogManager logManager)
{
    public async Task PrepareAsync(string workspacePath, CancellationToken cancellationToken)
    {
        await runner.RunAsync(new CommandSpec
        {
            FileName = "code",
            Arguments = ["--install-extension", "ms-vscode.cpptools", "--force"],
            Timeout = TimeSpan.FromMinutes(4),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        await extensionManager.InstallLocalExtensionAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await ApplyWorkspaceSettingsAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        await logManager.WriteAsync("info", "vscode", "VS Code preparado con C/C++, F9 y extension local.", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task OpenWorkspaceAsync(string workspacePath, CancellationToken cancellationToken)
    {
        await runner.RunAsync(new CommandSpec
        {
            FileName = "code",
            Arguments = [workspacePath],
            Timeout = TimeSpan.FromSeconds(20),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);
    }

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
    public async Task<string> PrepareAsync(string? requestedPath, string localAlias, CancellationToken cancellationToken)
    {
        var workspacePath = requestedPath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            workspacePath = paths.RepoRoot ?? paths.DefaultWorkspacePath;
        }

        Directory.CreateDirectory(workspacePath);
        RequireWorkspaceShape(workspacePath);

        await File.WriteAllTextAsync(Path.Combine(workspacePath, ".estudio_usuario"), Slugify(localAlias), cancellationToken)
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
        var manifest = await manifestManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        await manifestManager.SaveAsync(manifest with { WorkspacePath = workspacePath }, cancellationToken).ConfigureAwait(false);
        await logManager.WriteAsync("info", "workspace", $"Workspace preparado: {workspacePath}", cancellationToken)
            .ConfigureAwait(false);
        return workspacePath;
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

    private static string Slugify(string value)
    {
        var slug = new string(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? Environment.UserName.ToLowerInvariant() : slug;
    }
}

public sealed class TelemetryCompatibilityManager(ICommandRunner runner, AppPaths paths, LogManager logManager)
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
            Arguments = ["--inline", probe],
            WorkingDirectory = workspacePath,
            Environment = new Dictionary<string, string?> { ["ESTUDIO_SKIP_COMMIT"] = "1" },
            Timeout = TimeSpan.FromMinutes(2),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("El flujo real build.cmd/compilar_y_grabar.bat no valido correctamente.");
        }

        await logManager.WriteAsync("info", "telemetry", "Flujo compilar y grabar validado con probe.", cancellationToken)
            .ConfigureAwait(false);
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
