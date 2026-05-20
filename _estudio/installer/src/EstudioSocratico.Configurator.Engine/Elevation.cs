using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class ElevatedWorkerClient(LogManager logManager)
{
    public bool IsAvailable()
    {
        return File.Exists(GetWorkerPath());
    }

    public async Task<ElevatedOperationResult> RunAsync(ElevatedOperationRequest request, CancellationToken cancellationToken)
    {
        var workerPath = GetWorkerPath();
        if (!File.Exists(workerPath))
        {
            return new ElevatedOperationResult
            {
                Succeeded = false,
                Error = new InstallerError
                {
                    Code = InstallerErrorCode.ELEVATION_REQUIRED,
                    Title = "Se requiere elevacion",
                    Description = "No se encontro el worker elevado junto al configurador.",
                    ProbableCause = "La app se ejecuto desde salida de desarrollo incompleta.",
                    RecommendedAction = "Publica la app o ejecuta el bundle WiX antes de esta operacion."
                }
            };
        }

        var requestJson = JsonSerializer.Serialize(request, JsonDefaults.Options);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        var encoded = Convert.ToBase64String(requestBytes);
        var resultPath = Path.Combine(Path.GetTempPath(), $"estudio-elevated-{Guid.NewGuid():N}.json");

        var startInfo = new ProcessStartInfo
        {
            FileName = workerPath,
            UseShellExecute = true,
            Verb = "runas"
        };
        startInfo.ArgumentList.Add("--request");
        startInfo.ArgumentList.Add(encoded);
        startInfo.ArgumentList.Add("--result");
        startInfo.ArgumentList.Add(resultPath);

        await logManager.WriteAsync("info", "elevation", $"Solicitando elevacion para {request.Operation}", cancellationToken)
            .ConfigureAwait(false);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ElevatedOperationResult
            {
                Succeeded = false,
                Error = new InstallerError
                {
                    Code = InstallerErrorCode.ELEVATION_REQUIRED,
                    Title = "No se pudo abrir UAC",
                    Description = "Windows no inicio el worker elevado.",
                    ProbableCause = "El usuario cancelo UAC o Windows bloqueo la ejecucion.",
                    RecommendedAction = "Vuelve a intentar y acepta el aviso de permisos."
                }
            };
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (File.Exists(resultPath))
        {
            var json = await File.ReadAllTextAsync(resultPath, cancellationToken).ConfigureAwait(false);
            File.Delete(resultPath);
            return JsonSerializer.Deserialize<ElevatedOperationResult>(json, JsonDefaults.Options) ??
                   new ElevatedOperationResult { Succeeded = false, Message = "Respuesta elevada invalida." };
        }

        return new ElevatedOperationResult
        {
            Succeeded = false,
            Error = new InstallerError
            {
                Code = InstallerErrorCode.ELEVATION_REQUIRED,
                Title = "Operacion elevada incompleta",
                Description = $"El worker termino con codigo {process.ExitCode}, pero no devolvio resultado.",
                ProbableCause = "La operacion elevada fallo antes de escribir su reporte.",
                RecommendedAction = "Abre los logs y vuelve a intentar."
            }
        };
    }

    private static string GetWorkerPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "EstudioSocratico.Configurator.Elevated.exe");
    }
}
