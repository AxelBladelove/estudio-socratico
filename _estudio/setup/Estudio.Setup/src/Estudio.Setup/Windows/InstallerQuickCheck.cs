using System.ComponentModel;

namespace Estudio.Setup.Windows;

public sealed class InstallerQuickCheck : INotifyPropertyChanged
{
    private bool _isReady;
    private string _message;

    public InstallerQuickCheck(string title, bool isReady, string message)
    {
        Title = title;
        _isReady = isReady;
        _message = message;
    }

    public string Title { get; }

    public bool IsReady
    {
        get => _isReady;
        set
        {
            if (_isReady == value)
            {
                return;
            }

            _isReady = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReady)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusGlyph)));
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            if (string.Equals(_message, value, StringComparison.Ordinal))
            {
                return;
            }

            _message = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        }
    }

    public string StatusGlyph => IsReady ? "✓" : "!";

    public event PropertyChangedEventHandler? PropertyChanged;
}