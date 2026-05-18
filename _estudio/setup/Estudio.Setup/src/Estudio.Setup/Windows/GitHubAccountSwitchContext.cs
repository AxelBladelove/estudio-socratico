namespace Estudio.Setup.Windows;

public sealed record GitHubAccountSwitchContext(string? ActiveUserName, IReadOnlyList<string> KnownUsers)
{
    public int KnownAccountCount => KnownUsers.Count;
    public bool HasMultipleAccounts => KnownAccountCount > 1;
    public bool CanSwitchAutomatically => KnownAccountCount == 2;
}