namespace Estudio.Setup.Windows;

public sealed class InstallerNavigationStep
{
    public InstallerNavigationStep(int number, string title, string description, bool isActive, bool isCompleted)
    {
        Number = number;
        Title = title;
        Description = description;
        IsActive = isActive;
        IsCompleted = isCompleted;
    }

    public int Number { get; }
    public string Title { get; }
    public string Description { get; }
    public bool IsActive { get; }
    public bool IsCompleted { get; }
    public bool IsFuture => !IsActive && !IsCompleted;
    public string StatusGlyph => IsCompleted ? "✓" : Number.ToString();
}