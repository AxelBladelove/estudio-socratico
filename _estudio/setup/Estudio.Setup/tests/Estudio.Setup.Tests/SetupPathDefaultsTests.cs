using Estudio.Setup.State;

namespace Estudio.Setup.Tests;

public class SetupPathDefaultsTests
{
    [Fact]
    public void ResolveStateRoot_prefers_explicit_path()
    {
        var root = SetupPathDefaults.ResolveStateRoot("D:\\Estado", localAppData: "C:\\Users\\A\\AppData\\Local");

        Assert.Equal("D:\\Estado", root);
    }

    [Fact]
    public void ResolveStateRoot_uses_local_app_data_estudio_folder_when_no_path_is_given()
    {
        var root = SetupPathDefaults.ResolveStateRoot(null, localAppData: "C:\\Users\\A\\AppData\\Local");

        Assert.Equal("C:\\Users\\A\\AppData\\Local\\EstudioSocratico", root);
    }

    [Fact]
    public void ResolveLogRoot_places_logs_under_state_root()
    {
        var root = SetupPathDefaults.ResolveLogRoot("C:\\Users\\A\\AppData\\Local\\EstudioSocratico");

        Assert.Equal("C:\\Users\\A\\AppData\\Local\\EstudioSocratico\\logs", root);
    }
}
