using Estudio.Setup.Tui;

namespace Estudio.Setup.Tests;

public sealed class SetupTuiThemeTests
{
    [Fact]
    public void Scheme_names_are_estudio_specific_instead_of_terminal_gui_defaults()
    {
        var names = new[]
        {
            SetupTuiTheme.Background,
            SetupTuiTheme.Header,
            SetupTuiTheme.Panel,
            SetupTuiTheme.Surface,
            SetupTuiTheme.Accent,
            SetupTuiTheme.Muted,
        };

        Assert.All(names, name => Assert.StartsWith("Estudio.", name));
        Assert.DoesNotContain("Base", names);
        Assert.DoesNotContain("Dialog", names);
        Assert.DoesNotContain("Menu", names);
    }
}
