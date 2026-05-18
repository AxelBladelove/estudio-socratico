using System.ComponentModel;
using Estudio.Setup.Core;

namespace Estudio.Setup.Windows;

public sealed class InstallerProgressBlock : INotifyPropertyChanged
{
    private SetupExecutionBlockStatus _status;
    private string _humanMessage;
    private string _technicalMessage;

    public InstallerProgressBlock(string id, string title, string activeMessage)
    {
        Id = id;
        Title = title;
        ActiveMessage = activeMessage;
        _humanMessage = activeMessage;
        _technicalMessage = string.Empty;
        _status = SetupExecutionBlockStatus.Pending;
    }

    public string Id { get; }
    public string Title { get; }
    public string ActiveMessage { get; }

    public SetupExecutionBlockStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            Notify(nameof(Status));
            Notify(nameof(StatusLabel));
        }
    }

    public string HumanMessage
    {
        get => _humanMessage;
        set
        {
            if (string.Equals(_humanMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _humanMessage = value;
            Notify(nameof(HumanMessage));
        }
    }

    public string TechnicalMessage
    {
        get => _technicalMessage;
        set
        {
            if (string.Equals(_technicalMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _technicalMessage = value;
            Notify(nameof(TechnicalMessage));
        }
    }

    public string StatusLabel => Status switch
    {
        SetupExecutionBlockStatus.Ready => "Listo",
        SetupExecutionBlockStatus.Applied => "Completado",
        SetupExecutionBlockStatus.Repaired => "Reparado",
        SetupExecutionBlockStatus.Pending => "Pendiente",
        SetupExecutionBlockStatus.Warning => "Atencion",
        SetupExecutionBlockStatus.Failed => "Error",
        _ => "Pendiente",
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}