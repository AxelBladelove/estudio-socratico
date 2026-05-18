using Estudio.Setup.Security;

namespace Estudio.Setup.Windows;

public static class InstallerUiExceptionSupport
{
    public static InstallerUiErrorState CreateInitialReviewFailure(
        Exception exception,
        string? logRoot = null,
        Func<DateTimeOffset>? clock = null)
    {
        return CreateState(
            exception,
            "No pude continuar con la revisión inicial.",
            "Puedes intentar de nuevo o abrir los detalles técnicos.",
            logRoot,
            clock);
    }

    public static InstallerUiErrorState CreateUnexpectedFailure(
        Exception exception,
        string? logRoot = null,
        Func<DateTimeOffset>? clock = null)
    {
        return CreateState(
            exception,
            "No pude continuar con la instalación.",
            "Puedes intentar de nuevo o abrir los detalles técnicos.",
            logRoot,
            clock);
    }

    public static async Task<bool> TryRunAsync(
        Func<Task> action,
        Func<Exception, InstallerUiErrorState> errorFactory,
        Func<InstallerUiErrorState, Task> onFailure)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception exception)
        {
            await onFailure(errorFactory(exception));
            return false;
        }
    }

    private static InstallerUiErrorState CreateState(
        Exception exception,
        string headline,
        string body,
        string? logRoot,
        Func<DateTimeOffset>? clock)
    {
        var logPath = WriteRedactedLog(exception, logRoot, clock);
        var technicalDetails = SensitiveDataRedactor.Redact(exception.ToString());
        technicalDetails = string.Join(
            Environment.NewLine + Environment.NewLine,
            technicalDetails,
            $"Log local: {logPath}");
        return new InstallerUiErrorState(headline, body, technicalDetails, logPath);
    }

    internal static string WriteRedactedLog(
        Exception exception,
        string? logRoot = null,
        Func<DateTimeOffset>? clock = null)
    {
        var effectiveClock = clock ?? (() => DateTimeOffset.Now);
        var effectiveRoot = logRoot;
        if (string.IsNullOrWhiteSpace(effectiveRoot))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            effectiveRoot = Path.Combine(localAppData, "EstudioSocratico", "logs");
        }

        Directory.CreateDirectory(effectiveRoot);
        var now = effectiveClock();
        var path = Path.Combine(effectiveRoot, $"windows-installer-{now:yyyy-MM-dd}.log");
        var contents = SensitiveDataRedactor.Redact(exception.ToString());
        File.AppendAllText(path, $"[{now:yyyy-MM-dd HH:mm:ss zzz}] {contents}{Environment.NewLine}{Environment.NewLine}");
        return path;
    }
}