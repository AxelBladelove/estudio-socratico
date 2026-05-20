using System.Diagnostics;
using System.Text;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default);
}

public sealed class ProcessCommandRunner(LogManager? logManager = null) : ICommandRunner
{
    public async Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spec.FileName))
        {
            throw new ArgumentException("Command file name is required.", nameof(spec));
        }

        await (logManager?.WriteCommandAsync(spec, null, cancellationToken) ?? Task.CompletedTask).ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in spec.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (!string.IsNullOrWhiteSpace(spec.WorkingDirectory))
        {
            startInfo.WorkingDirectory = spec.WorkingDirectory;
        }

        foreach (var item in spec.Environment)
        {
            if (item.Value is null)
            {
                startInfo.Environment.Remove(item.Key);
            }
            else
            {
                startInfo.Environment[item.Key] = item.Value;
            }
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(spec.Timeout);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            stopwatch.Stop();
            var timedOut = new CommandResult
            {
                Spec = spec,
                ExitCode = -1,
                StandardOutput = SecretRedactor.Redact(stdout.ToString()),
                StandardError = "Command timed out.",
                Duration = stopwatch.Elapsed,
                TimedOut = true
            };
            await (logManager?.WriteCommandAsync(spec, timedOut, cancellationToken) ?? Task.CompletedTask)
                .ConfigureAwait(false);
            return timedOut;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var failed = new CommandResult
            {
                Spec = spec,
                ExitCode = -1,
                StandardOutput = SecretRedactor.Redact(stdout.ToString()),
                StandardError = SecretRedactor.Redact(ex.Message),
                Duration = stopwatch.Elapsed
            };
            await (logManager?.WriteCommandAsync(spec, failed, cancellationToken) ?? Task.CompletedTask)
                .ConfigureAwait(false);
            return failed;
        }

        stopwatch.Stop();
        var result = new CommandResult
        {
            Spec = spec,
            ExitCode = process.ExitCode,
            StandardOutput = SecretRedactor.Redact(stdout.ToString()),
            StandardError = SecretRedactor.Redact(stderr.ToString()),
            Duration = stopwatch.Elapsed
        };
        await (logManager?.WriteCommandAsync(spec, result, cancellationToken) ?? Task.CompletedTask)
            .ConfigureAwait(false);
        return result;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup after timeout.
        }
    }
}
