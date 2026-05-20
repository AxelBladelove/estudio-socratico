using System.IO.Compression;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class WingetBroker(ICommandRunner runner, DependencyDetector detector, LogManager logManager)
{
    public async Task<bool> IsReliableAsync(CancellationToken cancellationToken)
    {
        var winget = DependencyDetector.Requirements.Single(x => x.Id == DependencyId.Winget);
        var state = await detector.DetectAsync(winget, cancellationToken).ConfigureAwait(false);
        if (state.Status != DependencyStatus.Ready && state.Status != DependencyStatus.Outdated)
        {
            return false;
        }

        var source = await runner.RunAsync(new CommandSpec
        {
            FileName = state.Path ?? "winget",
            Arguments = ["source", "list"],
            Timeout = TimeSpan.FromSeconds(45),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        var ok = source.Succeeded && source.StandardOutput.Contains("winget", StringComparison.OrdinalIgnoreCase);
        await logManager.WriteAsync(ok ? "info" : "warn", "winget", ok ? "WinGet responde correctamente." : "WinGet no esta confiable.", cancellationToken)
            .ConfigureAwait(false);
        return ok;
    }

    public async Task<CommandResult> InstallPackageAsync(string packageId, CancellationToken cancellationToken)
    {
        return await runner.RunAsync(new CommandSpec
        {
            FileName = "winget",
            Arguments =
            [
                "install",
                "--id", packageId,
                "--exact",
                "--source", "winget",
                "--accept-package-agreements",
                "--accept-source-agreements",
                "--silent"
            ],
            Timeout = TimeSpan.FromMinutes(40),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DependencyInstaller(
    AppPaths paths,
    DependencyDetector detector,
    WingetBroker winget,
    OfficialInstallerFallback fallback,
    DownloadManager downloadManager,
    ChecksumVerifier checksumVerifier,
    PathManager pathManager,
    ManifestManager manifestManager,
    ElevatedWorkerClient elevatedWorker,
    LogManager logManager,
    SystemProbe systemProbe)
{
    public async Task<DependencyState> EnsureAsync(DependencyRequirement requirement, CancellationToken cancellationToken)
    {
        var before = await detector.DetectAsync(requirement, cancellationToken).ConfigureAwait(false);
        if (before.Status == DependencyStatus.Ready)
        {
            return before;
        }

        if (requirement.ManagedThroughMsys2)
        {
            var msys2 = new Msys2Manager(this, detector, pathManager, manifestManager, logManager);
            await msys2.EnsureToolchainAsync(cancellationToken).ConfigureAwait(false);
            var afterMsys = await detector.DetectAsync(requirement, cancellationToken).ConfigureAwait(false);
            await manifestManager.RecordDependencyAsync(before, afterMsys, afterMsys.Status == DependencyStatus.Ready, "msys2-pacman", null, cancellationToken)
                .ConfigureAwait(false);
            return afterMsys;
        }

        var installedByEstudio = false;
        var installerSource = "winget";
        string? sha = null;

        if (!string.IsNullOrWhiteSpace(requirement.WingetId) && await winget.IsReliableAsync(cancellationToken).ConfigureAwait(false))
        {
            var result = await winget.InstallPackageAsync(requirement.WingetId, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await logManager.WriteAsync("warn", requirement.Id.ToString(), $"WinGet fallo para {requirement.DisplayName}; intentando fallback oficial.", cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                installedByEstudio = true;
            }
        }

        var afterWinget = await detector.DetectAsync(requirement, cancellationToken).ConfigureAwait(false);
        if (afterWinget.Status != DependencyStatus.Ready)
        {
            installerSource = "official-fallback";
            var source = await fallback.ResolveAsync(requirement.Id, cancellationToken).ConfigureAwait(false);
            var fileName = Path.GetFileName(source.Uri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"{requirement.Id}.installer";
            }
            if (!Path.HasExtension(fileName) && requirement.Id == DependencyId.VSCode)
            {
                fileName = "VSCodeUserSetup-x64.exe";
            }

            var installerPath = await downloadManager.DownloadAsync(source.Uri, fileName, cancellationToken).ConfigureAwait(false);
            await checksumVerifier.VerifySha256Async(installerPath, source.Sha256, cancellationToken).ConfigureAwait(false);
            sha = await checksumVerifier.ComputeSha256Async(installerPath, cancellationToken).ConfigureAwait(false);
            await RunOfficialInstallerAsync(requirement, source, installerPath, cancellationToken).ConfigureAwait(false);
            installedByEstudio = true;
        }

        if (requirement.Id == DependencyId.Msys2)
        {
            await pathManager.EnsureUserPathEntryAsync(ProductInfo.DefaultMsys2UcrtBin, manifestManager, cancellationToken)
                .ConfigureAwait(false);
        }

        var after = await detector.DetectAsync(requirement, cancellationToken).ConfigureAwait(false);
        await manifestManager.RecordDependencyAsync(before, after, installedByEstudio, installerSource, sha, cancellationToken)
            .ConfigureAwait(false);

        return after.Status == DependencyStatus.Ready
            ? after
            : after with
            {
                Status = DependencyStatus.Failed,
                Error = new InstallerError
                {
                    Code = requirement.Id switch
                    {
                        DependencyId.Msys2 => InstallerErrorCode.MSYS2_INSTALL_FAILED,
                        DependencyId.Gcc => InstallerErrorCode.GCC_VALIDATION_FAILED,
                        DependencyId.Make => InstallerErrorCode.MAKE_VALIDATION_FAILED,
                        _ => InstallerErrorCode.COMMAND_FAILED
                    },
                    Title = $"{requirement.DisplayName} no quedo validado",
                    Description = "La instalacion termino, pero la validacion posterior fallo.",
                    ProbableCause = "PATH roto, instalacion incompleta o bloqueo de permisos.",
                    RecommendedAction = "Ejecuta Reparar. Si el problema persiste, exporta diagnostico."
                }
            };
    }

    private async Task RunOfficialInstallerAsync(
        DependencyRequirement requirement,
        OfficialInstallerSource source,
        string installerPath,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(installerPath).ToLowerInvariant();

        if (extension == ".zip" && requirement.Id == DependencyId.ExercismCli)
        {
            InstallExercismZip(installerPath);
            await pathManager.EnsureUserPathEntryAsync(pathManager.GetManagedToolsPath(), manifestManager, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (source.RequiresElevation && !systemProbe.IsRunningElevated())
        {
            var elevated = await elevatedWorker.RunAsync(new ElevatedOperationRequest
            {
                Operation = ElevatedOperationCode.RunOfficialInstaller,
                Parameters =
                {
                    ["path"] = installerPath,
                    ["arguments"] = string.Join("\u001f", BuildInstallerArguments(extension, installerPath, source))
                }
            }, cancellationToken).ConfigureAwait(false);

            if (!elevated.Succeeded)
            {
                throw new InvalidOperationException(elevated.Error?.Description ?? elevated.Message);
            }

            return;
        }

        var spec = BuildInstallerCommand(extension, installerPath, source);
        var result = await new ProcessCommandRunner(logManager).RunAsync(spec, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Instalador oficial fallo para {requirement.DisplayName}: {result.StandardError}");
        }
    }

    private void InstallExercismZip(string zipPath)
    {
        var temp = Path.Combine(paths.DownloadCache, "exercism-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        ZipFile.ExtractToDirectory(zipPath, temp);
        var exe = Directory.GetFiles(temp, "exercism.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (exe is null)
        {
            throw new InvalidOperationException("El ZIP oficial de Exercism no contenia exercism.exe.");
        }

        var target = Path.Combine(pathManager.GetManagedToolsPath(), "exercism.exe");
        File.Copy(exe, target, overwrite: true);
        Directory.Delete(temp, recursive: true);
    }

    private static CommandSpec BuildInstallerCommand(string extension, string installerPath, OfficialInstallerSource source)
    {
        return extension switch
        {
            ".msi" => new CommandSpec
            {
                FileName = "msiexec.exe",
                Arguments = BuildInstallerArguments(extension, installerPath, source),
                Timeout = TimeSpan.FromMinutes(40),
                AllowNonZeroExitCode = true
            },
            _ => new CommandSpec
            {
                FileName = installerPath,
                Arguments = BuildInstallerArguments(extension, installerPath, source),
                Timeout = TimeSpan.FromMinutes(40),
                AllowNonZeroExitCode = true
            }
        };
    }

    private static IReadOnlyList<string> BuildInstallerArguments(string extension, string installerPath, OfficialInstallerSource source)
    {
        if (extension == ".msi")
        {
            return ["/i", installerPath, .. source.SilentArguments];
        }

        return source.SilentArguments;
    }
}

public sealed class Msys2Manager(
    DependencyInstaller dependencyInstaller,
    DependencyDetector detector,
    PathManager pathManager,
    ManifestManager manifestManager,
    LogManager logManager)
{
    public async Task<DependencyState> EnsureMsys2Async(CancellationToken cancellationToken)
    {
        var requirement = DependencyDetector.Requirements.Single(x => x.Id == DependencyId.Msys2);
        var state = await detector.DetectAsync(requirement, cancellationToken).ConfigureAwait(false);
        if (state.Status == DependencyStatus.Ready)
        {
            await pathManager.EnsureUserPathEntryAsync(ProductInfo.DefaultMsys2UcrtBin, manifestManager, cancellationToken)
                .ConfigureAwait(false);
            return state;
        }

        return await dependencyInstaller.EnsureAsync(requirement, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureToolchainAsync(CancellationToken cancellationToken)
    {
        await EnsureMsys2Async(cancellationToken).ConfigureAwait(false);
        var bash = Path.Combine(ProductInfo.DefaultMsys2Root, "usr", "bin", "bash.exe");
        if (!File.Exists(bash))
        {
            throw new InvalidOperationException("MSYS2 no esta instalado en C:\\msys64.");
        }

        var result = await new ProcessCommandRunner(logManager).RunAsync(new CommandSpec
        {
            FileName = bash,
            Arguments =
            [
                "-lc",
                "pacman -Sy --needed --noconfirm mingw-w64-ucrt-x86_64-gcc mingw-w64-ucrt-x86_64-make make"
            ],
            Timeout = TimeSpan.FromMinutes(30),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("pacman no pudo instalar GCC/Make UCRT64.");
        }

        EnsureMakeAlias();
        await pathManager.EnsureUserPathEntryAsync(ProductInfo.DefaultMsys2UcrtBin, manifestManager, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void EnsureMakeAlias()
    {
        var make = Path.Combine(ProductInfo.DefaultMsys2UcrtBin, "make.exe");
        var mingwMake = Path.Combine(ProductInfo.DefaultMsys2UcrtBin, "mingw32-make.exe");
        if (!File.Exists(make) && File.Exists(mingwMake))
        {
            File.Copy(mingwMake, make, overwrite: true);
        }
    }
}

public sealed class GccManager(DependencyDetector detector, LogManager logManager)
{
    public async Task<DependencyState> ValidateAsync(CancellationToken cancellationToken)
    {
        var requirement = DependencyDetector.Requirements.Single(x => x.Id == DependencyId.Gcc);
        var state = await detector.DetectAsync(requirement, cancellationToken).ConfigureAwait(false);
        if (state.Status != DependencyStatus.Ready || state.Path is null)
        {
            return state with
            {
                Error = new InstallerError
                {
                    Code = InstallerErrorCode.GCC_VALIDATION_FAILED,
                    Title = "GCC no esta listo",
                    Description = "No se encontro gcc UCRT64 funcional.",
                    ProbableCause = "MSYS2 no tiene mingw-w64-ucrt-x86_64-gcc instalado o PATH apunta a otro GCC.",
                    RecommendedAction = "Ejecuta Reparar toolchain C."
                }
            };
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "estudio-gcc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var source = Path.Combine(tempDir, "hello.c");
        var exe = Path.Combine(tempDir, "hello.exe");
        await File.WriteAllTextAsync(source, "#include <stdio.h>\nint main(void){puts(\"ok\");return 0;}\n", cancellationToken)
            .ConfigureAwait(false);
        var result = await new ProcessCommandRunner(logManager).RunAsync(new CommandSpec
        {
            FileName = state.Path,
            Arguments = [source, "-o", exe, "-std=c99", "-Wall", "-Wextra"],
            Timeout = TimeSpan.FromSeconds(45),
            AllowNonZeroExitCode = true
        }, cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // Non-critical cleanup after compiler validation.
        }
        return result.Succeeded ? state : state with { Status = DependencyStatus.Broken };
    }
}

public sealed class MakeManager(DependencyDetector detector)
{
    public Task<DependencyState> ValidateAsync(CancellationToken cancellationToken)
    {
        var requirement = DependencyDetector.Requirements.Single(x => x.Id == DependencyId.Make);
        return detector.DetectAsync(requirement, cancellationToken);
    }
}

public sealed class NodeJsManager(DependencyInstaller installer)
{
    public Task<DependencyState> EnsureAsync(CancellationToken ct) =>
        installer.EnsureAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.NodeJs), ct);
}

public sealed class PythonManager(DependencyInstaller installer)
{
    public Task<DependencyState> EnsureAsync(CancellationToken ct) =>
        installer.EnsureAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.Python), ct);
}

public sealed class GitManager(DependencyInstaller installer)
{
    public Task<DependencyState> EnsureAsync(CancellationToken ct) =>
        installer.EnsureAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.Git), ct);
}

public sealed class GitHubCliManager(DependencyInstaller installer)
{
    public Task<DependencyState> EnsureAsync(CancellationToken ct) =>
        installer.EnsureAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.GitHubCli), ct);
}

public sealed class ExercismCliManager(DependencyInstaller installer)
{
    public Task<DependencyState> EnsureAsync(CancellationToken ct) =>
        installer.EnsureAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.ExercismCli), ct);
}

public sealed class VSCodeDependencyManager(DependencyInstaller installer)
{
    public Task<DependencyState> EnsureAsync(CancellationToken ct) =>
        installer.EnsureAsync(DependencyDetector.Requirements.Single(x => x.Id == DependencyId.VSCode), ct);
}
