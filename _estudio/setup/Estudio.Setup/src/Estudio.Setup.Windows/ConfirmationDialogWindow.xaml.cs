using System.Windows;

namespace Estudio.Setup.WindowsHost;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow(string headline, string body, string primaryButtonText, string secondaryButtonText)
    {
        Headline = headline;
        Body = body;
        PrimaryButtonText = primaryButtonText;
        SecondaryButtonText = secondaryButtonText;
        InitializeComponent();
        DataContext = this;
        SourceInitialized += OnSourceInitialized;
    }

    public string Headline { get; }
    public string Body { get; }
    public string PrimaryButtonText { get; }
    public string SecondaryButtonText { get; }

    public static bool Show(Window owner, string headline, string body, string primaryButtonText, string secondaryButtonText)
    {
        var dialog = new ConfirmationDialogWindow(headline, body, primaryButtonText, secondaryButtonText)
        {
            Owner = owner,
        };

        return dialog.ShowDialog() == true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowsTitleBarStyling.ApplyDarkMode(this);
    }

    private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}