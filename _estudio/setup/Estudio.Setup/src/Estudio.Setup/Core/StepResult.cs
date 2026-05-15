namespace Estudio.Setup.Core;

public sealed record StepResult(bool Success, bool IsMissing, string Message, bool IsWarning = false)
{
    public static StepResult Ok(string message) => new(true, false, message);

    public static StepResult Missing(string message) => new(false, true, message);

    public static StepResult Fail(string message) => new(false, false, message);

    public static StepResult Warning(string message) => new(true, false, message, true);

    public StepResult AsWarning()
    {
        return Warning(Message);
    }
}
