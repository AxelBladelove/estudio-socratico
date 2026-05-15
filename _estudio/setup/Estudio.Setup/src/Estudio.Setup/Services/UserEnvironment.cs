namespace Estudio.Setup.Services;

public sealed class UserEnvironment : IUserEnvironment
{
    public string? GetUserVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    }

    public void SetUserVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
    }
}
