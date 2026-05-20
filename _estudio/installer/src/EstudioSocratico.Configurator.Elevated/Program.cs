using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EstudioSocratico.Configurator.Core;
using Microsoft.Win32;

namespace EstudioSocratico.Configurator.Elevated;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var resultPath = GetOption(args, "--result");
        try
        {
            var encoded = GetOption(args, "--request") ?? throw new InvalidOperationException("Missing --request.");
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var request = JsonSerializer.Deserialize<ElevatedOperationRequest>(json, JsonDefaults.Options)
                          ?? throw new InvalidOperationException("Invalid elevated request.");
            var result = await DispatchAsync(request).ConfigureAwait(false);
            await WriteResultAsync(resultPath, result).ConfigureAwait(false);
            return result.Succeeded ? 0 : 1;
        }
        catch (Exception ex)
        {
            await WriteResultAsync(resultPath, new ElevatedOperationResult
            {
                Succeeded = false,
                Error = InstallerError.FromException(ex, InstallerErrorCode.ELEVATION_REQUIRED)
            }).ConfigureAwait(false);
            return 1;
        }
    }

    private static Task<ElevatedOperationResult> DispatchAsync(ElevatedOperationRequest request)
    {
        return request.Operation switch
        {
            ElevatedOperationCode.AddMachinePath => AddMachinePathAsync(request),
            ElevatedOperationCode.RepairPath => AddMachinePathAsync(request),
            ElevatedOperationCode.InstallWingetPackage => InstallWingetPackageAsync(request),
            ElevatedOperationCode.RunOfficialInstaller => RunOfficialInstallerAsync(request),
            ElevatedOperationCode.InstallMsys2 => RunOfficialInstallerAsync(request),
            ElevatedOperationCode.RemoveManagedDependency => RemoveManagedDependencyAsync(request),
            _ => Task.FromResult(Fail("Operacion elevada no permitida."))
        };
    }

    private static Task<ElevatedOperationResult> AddMachinePathAsync(ElevatedOperationRequest request)
    {
        var entry = RequireParameter(request, "path");
        if (!Directory.Exists(entry))
        {
            return Task.FromResult(Fail("La ruta que se quiere agregar al PATH de maquina no existe."));
        }

        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true)
                        ?? throw new InvalidOperationException("No se pudo abrir el registro de Environment.");
        var before = (string?)key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) ?? "";
        var entries = before.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!entries.Any(x => string.Equals(Path.GetFullPath(x).TrimEnd('\\'), Path.GetFullPath(entry).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
        {
            entries.Add(entry);
            key.SetValue("Path", string.Join(Path.PathSeparator, entries), RegistryValueKind.ExpandString);
        }

        return Task.FromResult(Ok("PATH de maquina actualizado."));
    }

    private static async Task<ElevatedOperationResult> InstallWingetPackageAsync(ElevatedOperationRequest request)
    {
        var packageId = RequireParameter(request, "packageId");
        if (!Regex.IsMatch(packageId, @"^[A-Za-z0-9_.-]+$"))
        {
            return Fail("packageId invalido.");
        }

        var result = await RunProcessAsync("winget",
        [
            "install",
            "--id", packageId,
            "--exact",
            "--source", "winget",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--silent"
        ]).ConfigureAwait(false);
        return result == 0 ? Ok("Paquete WinGet instalado.") : Fail($"winget devolvio {result}.");
    }

    private static async Task<ElevatedOperationResult> RunOfficialInstallerAsync(ElevatedOperationRequest request)
    {
        var path = RequireParameter(request, "path");
        if (!File.Exists(path))
        {
            return Fail("El instalador oficial no existe.");
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".exe", ".msi" };
        var extension = Path.GetExtension(path);
        if (!allowedExtensions.Contains(extension))
        {
            return Fail("Extension de instalador no permitida para elevacion.");
        }

        var rawArgs = request.Parameters.TryGetValue("arguments", out var value) ? value : "";
        var args = rawArgs.Split('\u001f', StringSplitOptions.RemoveEmptyEntries).ToList();
        var exitCode = extension.Equals(".msi", StringComparison.OrdinalIgnoreCase)
            ? await RunProcessAsync("msiexec.exe", args).ConfigureAwait(false)
            : await RunProcessAsync(path, args).ConfigureAwait(false);

        return exitCode == 0 ? Ok("Instalador oficial completado.") : Fail($"Instalador oficial devolvio {exitCode}.");
    }

    private static Task<ElevatedOperationResult> RemoveManagedDependencyAsync(ElevatedOperationRequest request)
    {
        var target = RequireParameter(request, "path");
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var allowedRoot = Path.Combine(localAppData, ProductInfo.AppDataFolderName);
        PathSafety.RequireInside(allowedRoot, target, "elevated remove");
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
        else if (File.Exists(target))
        {
            File.Delete(target);
        }

        return Task.FromResult(Ok("Elemento gestionado eliminado."));
    }

    private static async Task<int> RunProcessAsync(string fileName, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"No se pudo iniciar {fileName}.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }

    private static string RequireParameter(ElevatedOperationRequest request, string name)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Parametro requerido ausente: {name}");
        }

        return value;
    }

    private static ElevatedOperationResult Ok(string message) => new() { Succeeded = true, Message = message };

    private static ElevatedOperationResult Fail(string message) => new()
    {
        Succeeded = false,
        Message = message,
        Error = new InstallerError
        {
            Code = InstallerErrorCode.ELEVATION_REQUIRED,
            Title = "Operacion elevada fallida",
            Description = message,
            ProbableCause = "Permisos, instalador bloqueado o parametros rechazados.",
            RecommendedAction = "Reintenta la accion desde el configurador y revisa logs."
        }
    };

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static async Task WriteResultAsync(string? resultPath, ElevatedOperationResult result)
    {
        if (string.IsNullOrWhiteSpace(resultPath))
        {
            return;
        }

        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, JsonDefaults.Options)).ConfigureAwait(false);
    }
}
