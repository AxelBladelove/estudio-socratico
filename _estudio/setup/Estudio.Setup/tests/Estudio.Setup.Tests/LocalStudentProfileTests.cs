using Estudio.Setup.Profile;

namespace Estudio.Setup.Tests;

public class LocalStudentProfileTests
{
    [Fact]
    public async Task ResolveAlias_reads_estudio_usuario_from_workspace_root()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "axel\r\n");

        var alias = LocalStudentProfile.ResolveAlias(root);

        Assert.Equal("axel", alias);
    }

    [Theory]
    [InlineData("axel")]
    [InlineData("axel_02")]
    [InlineData("axel-02")]
    [InlineData("Axel02")]
    public void ValidateAlias_accepts_supported_alias_format(string alias)
    {
        LocalStudentProfile.ValidateAlias(alias);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("axel 02")]
    [InlineData("-axel")]
    [InlineData("axel-")]
    [InlineData("axel.02")]
    [InlineData("áxel")]
    public void ValidateAlias_rejects_aliases_that_cannot_be_used_for_forks_or_folders(string alias)
    {
        var error = Assert.Throws<ArgumentException>(() => LocalStudentProfile.ValidateAlias(alias));

        Assert.Contains("alias", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveAlias_returns_fallback_when_identity_file_is_missing()
    {
        var alias = LocalStudentProfile.ResolveAlias(MakeTempRoot());

        Assert.Equal("estudiante", alias);
    }

    [Fact]
    public async Task ResolveAlias_rejects_invalid_identity_file_alias()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "axel 02");

        var error = Assert.Throws<ArgumentException>(() => LocalStudentProfile.ResolveAlias(root));

        Assert.Contains(".estudio_usuario", error.Message);
    }

    [Fact]
    public async Task FindWorkspaceRoot_walks_up_until_identity_file_exists()
    {
        var root = MakeTempRoot();
        var nested = Path.Combine(root, "_estudio", "setup");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "axel");

        var found = LocalStudentProfile.FindWorkspaceRoot(nested);

        Assert.Equal(Path.GetFullPath(root), found);
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }
}
