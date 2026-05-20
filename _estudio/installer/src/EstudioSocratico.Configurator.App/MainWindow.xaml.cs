using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace EstudioSocratico.Configurator.App;

public sealed partial class MainWindow : Window
{
    private readonly ConfiguratorEngine _engine = new();
    private readonly WebViewBridge _bridge;

    public MainWindow()
    {
        InitializeComponent();
        _bridge = new WebViewBridge(_engine);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        ConfigureWindow();
        _ = InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            // Set WebView2 user data folder via environment variable before init.
            // WinUI 3's WebView2 control creates its environment internally.
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EstudioSocratico", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);

            await AppWebView.EnsureCoreWebView2Async();

            var webView = AppWebView.CoreWebView2;

            // Security: restrict navigation and features
            var settings = webView.Settings;
#if !DEBUG
            settings.AreDevToolsEnabled = false;
#endif
            settings.AreHostObjectsAllowed = false;
            settings.IsStatusBarEnabled = false;
            settings.IsWebMessageEnabled = true;

            // Prevent navigation to external URLs
            webView.NavigationStarting += (sender, args) =>
            {
                if (args.Uri != null && !args.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    && !args.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
            };

            // Block new window requests (popups)
            webView.NewWindowRequested += (sender, args) =>
            {
                // Open in default browser instead
                if (!string.IsNullOrEmpty(args.Uri))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri)
                    {
                        UseShellExecute = true
                    });
                }
                args.Handled = true;
            };

            // Attach the bridge for message handling
            _bridge.Attach(webView);

            // Navigate to the embedded React UI (Use Virtual Host to allow ES Modules)
            var wwwrootFolder = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var indexHtmlPath = Path.Combine(wwwrootFolder, "index.html");
            await _engine.Logs.WriteAsync("info", "webview", $"Inicializando WebView2 con wwwroot en '{wwwrootFolder}'.");
            if (File.Exists(indexHtmlPath))
            {
                webView.SetVirtualHostNameToFolderMapping(
                    "app.local", 
                    wwwrootFolder, 
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                await _engine.Logs.WriteAsync("info", "webview", $"Navegando a https://app.local/index.html usando '{indexHtmlPath}'.");
                webView.Navigate("https://app.local/index.html");
            }
            else
            {
                await _engine.Logs.WriteAsync("error", "webview", $"No se encontro index.html en '{indexHtmlPath}'.");
                // Fallback: show a message if UI files are missing
                webView.NavigateToString(GetFallbackHtml(indexHtmlPath));
            }
        }
        catch (Exception ex)
        {
            // If WebView2 fails to initialize, show error in a basic way
            AppWebView.Visibility = Visibility.Collapsed;
            await _engine.Logs.WriteErrorAsync(InstallerError.FromException(ex));
            await _engine.Logs.WriteAsync("error", "webview", $"WebView2 no pudo inicializarse: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex}");
        }
    }

    private void ConfigureWindow()
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 31, 41, 61);
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 45, 58, 83);
        }

        AppWindow.Resize(new SizeInt32(1120, 760));
    }

    private static string GetFallbackHtml(string expectedPath) => $@"
<!DOCTYPE html>
<html>
<head>
  <style>
    body {{
      background: #0f1419;
      color: #e2e8f0;
      font-family: 'Segoe UI', system-ui, sans-serif;
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100vh;
      margin: 0;
    }}
    .container {{
      text-align: center;
      max-width: 500px;
    }}
    h1 {{ color: #4fa3ff; font-size: 24px; }}
    p {{ color: #8b949e; line-height: 1.6; }}
    code {{
      background: #1c2333;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 13px;
      color: #f59e0b;
    }}
  </style>
</head>
<body>
  <div class='container'>
    <h1>UI no encontrada</h1>
    <p>No se encontró la interfaz React en:</p>
    <p><code>{expectedPath.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")}</code></p>
    <p>Ejecuta <code>scripts\build-ui.bat</code> en <code>_estudio/installer</code> para reconstruir y copiar la UI.</p>
  </div>
</body>
</html>";
}
