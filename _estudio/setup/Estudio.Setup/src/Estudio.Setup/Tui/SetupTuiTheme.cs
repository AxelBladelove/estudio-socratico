using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using GuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Estudio.Setup.Tui;

public static class SetupTuiTheme
{
    public const string Background = "Estudio.Background";
    public const string Header = "Estudio.Header";
    public const string Panel = "Estudio.Panel";
    public const string Surface = "Estudio.Surface";
    public const string Accent = "Estudio.Accent";
    public const string Muted = "Estudio.Muted";
    public const string Error = "Estudio.Error";

    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var background = Color.Parse("#101417", null);
        var header = Color.Parse("#1f6f78", null);
        var panel = Color.Parse("#151b1f", null);
        var surface = Color.Parse("#0f1417", null);
        var border = Color.Parse("#2f444d", null);
        var accent = Color.Parse("#8ee6d1", null);
        var focus = Color.Parse("#25474c", null);
        var foreground = Color.Parse("#e8ecef", null);
        var muted = Color.Parse("#97a4aa", null);
        var warning = Color.Parse("#f2c36b", null);
        var error = Color.Parse("#ff6b6b", null);

        SchemeManager.AddScheme(Background, new Scheme
        {
            Normal = Attr(foreground, background),
            HotNormal = Attr(accent, background, TextStyle.Bold),
            Focus = Attr(foreground, focus),
            HotFocus = Attr(accent, focus, TextStyle.Bold),
            Active = Attr(foreground, focus),
            HotActive = Attr(accent, focus, TextStyle.Bold),
            Highlight = Attr(foreground, focus),
            Editable = Attr(foreground, surface),
            ReadOnly = Attr(muted, surface),
            Disabled = Attr(muted, background),
        });

        SchemeManager.AddScheme(Header, new Scheme
        {
            Normal = Attr(foreground, header, TextStyle.Bold),
            HotNormal = Attr(accent, header, TextStyle.Bold),
            Focus = Attr(foreground, header, TextStyle.Bold),
            Active = Attr(foreground, header, TextStyle.Bold),
            Highlight = Attr(accent, header, TextStyle.Bold),
            Disabled = Attr(muted, header),
        });

        SchemeManager.AddScheme(Panel, new Scheme
        {
            Normal = Attr(foreground, panel),
            HotNormal = Attr(accent, panel, TextStyle.Bold),
            Focus = Attr(foreground, focus),
            HotFocus = Attr(accent, focus, TextStyle.Bold),
            Active = Attr(foreground, focus),
            HotActive = Attr(accent, focus, TextStyle.Bold),
            Highlight = Attr(accent, panel, TextStyle.Bold),
            Editable = Attr(foreground, surface),
            ReadOnly = Attr(muted, surface),
            Disabled = Attr(muted, panel),
            Code = Attr(border, panel),
        });

        SchemeManager.AddScheme(Surface, new Scheme
        {
            Normal = Attr(foreground, surface),
            HotNormal = Attr(accent, surface, TextStyle.Bold),
            Focus = Attr(foreground, focus),
            HotFocus = Attr(accent, focus, TextStyle.Bold),
            Active = Attr(foreground, focus),
            HotActive = Attr(accent, focus, TextStyle.Bold),
            Highlight = Attr(foreground, focus),
            Editable = Attr(foreground, surface),
            ReadOnly = Attr(muted, surface),
            Disabled = Attr(muted, surface),
            Code = Attr(accent, surface),
        });

        SchemeManager.AddScheme(Accent, new Scheme
        {
            Normal = Attr(accent, panel, TextStyle.Bold),
            HotNormal = Attr(foreground, panel, TextStyle.Bold),
            Focus = Attr(foreground, focus, TextStyle.Bold),
            HotFocus = Attr(accent, focus, TextStyle.Bold),
            Active = Attr(foreground, focus, TextStyle.Bold),
            HotActive = Attr(accent, focus, TextStyle.Bold),
            Highlight = Attr(foreground, focus, TextStyle.Bold),
            Disabled = Attr(muted, panel),
        });

        SchemeManager.AddScheme(Muted, new Scheme
        {
            Normal = Attr(muted, background),
            HotNormal = Attr(accent, background),
            Focus = Attr(foreground, focus),
            Active = Attr(warning, background, TextStyle.Bold),
            Highlight = Attr(accent, background),
            Disabled = Attr(muted, background),
        });

        SchemeManager.AddScheme(Error, new Scheme
        {
            Normal = Attr(error, background, TextStyle.Bold),
            HotNormal = Attr(error, background, TextStyle.Bold),
            Focus = Attr(foreground, error, TextStyle.Bold),
            Active = Attr(warning, background, TextStyle.Bold),
            Highlight = Attr(error, panel, TextStyle.Bold),
            Disabled = Attr(muted, background),
        });

        _registered = true;
    }

    private static GuiAttribute Attr(Color foreground, Color background, TextStyle style = TextStyle.None)
    {
        return new GuiAttribute(foreground, background, style);
    }
}
