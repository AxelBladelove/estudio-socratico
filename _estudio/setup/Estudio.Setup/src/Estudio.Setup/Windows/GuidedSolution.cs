namespace Estudio.Setup.Windows;

public sealed record GuidedSolution(
    string BlockId,
    string Title,
    string Summary,
    IReadOnlyList<string> Steps,
    string? ActionLabel = null,
    string? ActionUrl = null);