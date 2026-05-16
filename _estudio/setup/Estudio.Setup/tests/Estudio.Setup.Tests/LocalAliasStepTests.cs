using Estudio.Setup.Core;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class LocalAliasStepTests
{
    [Fact]
    public async Task VerifyAsync_succeeds_when_identity_file_matches_expected_alias()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "axel\r\n");
        var step = new LocalAliasStep(root, "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DetectAsync_returns_missing_when_identity_file_is_absent()
    {
        var step = new LocalAliasStep(MakeTempRoot(), "axel");

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public async Task InstallAsync_writes_identity_file_when_missing()
    {
        var root = MakeTempRoot();
        var step = new LocalAliasStep(root, "axel");

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("axel", (await File.ReadAllTextAsync(Path.Combine(root, ".estudio_usuario"))).Trim());
    }

    [Fact]
    public async Task UpdateAsync_backs_up_existing_alias_before_writing_new_alias()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "old");
        var step = new LocalAliasStep(
            root,
            "axel",
            now: () => new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero));

        var result = await step.UpdateAsync(new SetupContext(new SetupOptions(SetupMode.Update)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("axel", (await File.ReadAllTextAsync(Path.Combine(root, ".estudio_usuario"))).Trim());
        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(root, ".estudio_usuario.20260515120000.bak")));
    }

    [Fact]
    public async Task VerifyAsync_fails_when_identity_file_contains_invalid_alias()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "axel 02");
        var step = new LocalAliasStep(root, "axel");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("invalido", result.Message);
    }

    [Fact]
    public async Task DetectAsync_treats_invalid_identity_file_as_repairable_missing_alias()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "axel 02");
        var step = new LocalAliasStep(root, "axel");

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("reescribir", result.Message);
    }

    [Fact]
    public async Task InstallAsync_replaces_invalid_identity_file_with_backup()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".estudio_usuario"), "axel 02");
        var step = new LocalAliasStep(
            root,
            "axel",
            now: () => new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero));

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("axel", (await File.ReadAllTextAsync(Path.Combine(root, ".estudio_usuario"))).Trim());
        Assert.Equal("axel 02", await File.ReadAllTextAsync(Path.Combine(root, ".estudio_usuario.20260515120000.bak")));
    }

    [Fact]
    public async Task UninstallAsync_removes_identity_file_when_present()
    {
        var root = MakeTempRoot();
        Directory.CreateDirectory(root);
        var identityPath = Path.Combine(root, ".estudio_usuario");
        await File.WriteAllTextAsync(identityPath, "axel\r\n");
        var step = new LocalAliasStep(root, "axel");

        var result = await step.UninstallAsync(new SetupContext(new SetupOptions(SetupMode.Uninstall)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(identityPath));
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }
}
