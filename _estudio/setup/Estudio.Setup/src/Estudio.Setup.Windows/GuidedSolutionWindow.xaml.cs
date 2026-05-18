using System.Windows;
using Estudio.Setup.Windows;

namespace Estudio.Setup.WindowsHost;

public partial class GuidedSolutionWindow : Window
{
    private readonly Action<string?> _openExternalHelp;

    public GuidedSolutionWindow(GuidedSolution solution, Action<string?> openExternalHelp)
    {
        Solution = solution;
        _openExternalHelp = openExternalHelp;
        InitializeComponent();
        DataContext = this;
        CloseButton.Click += (_, _) => Close();
        ActionButton.Click += (_, _) => _openExternalHelp(Solution.ActionUrl);
        ActionButton.Visibility = string.IsNullOrWhiteSpace(Solution.ActionUrl) ? Visibility.Collapsed : Visibility.Visible;
    }

    public GuidedSolution Solution { get; }
}