using EstudioSocratico.Configurator.Core;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Redacts_GitHub_And_Exercism_Tokens()
    {
        var text = "ghp_abcdefghijklmnopqrstuvwxyz123456 exercism configure --token abcdefghijklmnopqrstuvwxyz";
        var redacted = SecretRedactor.Redact(text);
        Assert.DoesNotContain("ghp_", redacted);
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void Rejects_Delete_Path_Outside_Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "estudio-root");
        var outside = Path.Combine(Path.GetTempPath(), "outside");
        Assert.False(PathSafety.IsInside(root, outside));
    }
}
