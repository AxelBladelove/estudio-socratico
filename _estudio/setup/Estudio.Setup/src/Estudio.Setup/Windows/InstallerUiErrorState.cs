namespace Estudio.Setup.Windows;

public sealed record InstallerUiErrorState(
    string Headline,
    string Body,
    string TechnicalDetails,
    string LogPath,
    bool DetailsHiddenByDefault = true);