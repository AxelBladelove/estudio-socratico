namespace Estudio.Setup.Services;

public interface IUserEnvironment
{
    string? GetUserVariable(string name);

    void SetUserVariable(string name, string value);
}
