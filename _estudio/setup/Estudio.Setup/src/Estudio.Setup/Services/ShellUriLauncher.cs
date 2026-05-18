using System.Diagnostics;

namespace Estudio.Setup.Services;

public sealed class ShellUriLauncher : IUriLauncher
{
    public void Open(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        });
    }
}