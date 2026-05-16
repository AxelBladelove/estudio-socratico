using System.Text.Json;
using Estudio.Setup.Core;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class VsCodeSettingsStepTests
{
    [Fact]
    public async Task InstallAsync_preserves_existing_settings_and_writes_estudio_keys()
    {
        var root = MakeTempRoot();
        var settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(settingsPath, """{"editor.fontSize": 16, "estudioSocratico.alias": "old"}""");
        var step = new VsCodeSettingsStep(
            settingsPath,
            alias: "axel",
            configPath: @"%APPDATA%\EstudioSocratico\config.json");

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        Assert.Equal(16, document.RootElement.GetProperty("editor.fontSize").GetInt32());
        Assert.Equal("PowerShell 7", document.RootElement.GetProperty("terminal.integrated.defaultProfile.windows").GetString());
        Assert.Equal(
            "pwsh.exe",
            document.RootElement
                .GetProperty("terminal.integrated.profiles.windows")
                .GetProperty("PowerShell 7")
                .GetProperty("path")
                .GetString());
        Assert.Equal("axel", document.RootElement.GetProperty("estudioSocratico.alias").GetString());
        Assert.Equal(@"%APPDATA%\EstudioSocratico\config.json", document.RootElement.GetProperty("estudioSocratico.configPath").GetString());
        Assert.Single(Directory.GetFiles(root, "settings.json.*.bak"));
    }

    [Fact]
    public async Task InstallAsync_preserves_existing_terminal_profiles_when_adding_powershell7()
    {
        var root = MakeTempRoot();
        var settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "terminal.integrated.profiles.windows": {
                "Command Prompt": { "path": "cmd.exe" }
              }
            }
            """);
        var step = new VsCodeSettingsStep(
            settingsPath,
            alias: "axel",
            configPath: @"%APPDATA%\EstudioSocratico\config.json");

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        var profiles = document.RootElement.GetProperty("terminal.integrated.profiles.windows");
        Assert.Equal("cmd.exe", profiles.GetProperty("Command Prompt").GetProperty("path").GetString());
        Assert.Equal("pwsh.exe", profiles.GetProperty("PowerShell 7").GetProperty("path").GetString());
    }

    [Fact]
    public async Task InstallAsync_uses_numbered_backup_when_timestamp_backup_already_exists()
    {
        var root = MakeTempRoot();
        var settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(settingsPath, """{"editor.fontSize": 16}""");
        await File.WriteAllTextAsync(Path.Combine(root, "settings.json.20260515120000.bak"), "older backup");
        var step = new VsCodeSettingsStep(
            settingsPath,
            alias: "axel",
            configPath: @"%APPDATA%\EstudioSocratico\config.json",
            now: () => new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero));

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        var backupPath = Path.Combine(root, "settings.json.20260515120000.1.bak");
        Assert.True(File.Exists(backupPath));
        Assert.Equal("""{"editor.fontSize": 16}""", await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task DetectAsync_returns_missing_when_settings_are_absent()
    {
        var step = new VsCodeSettingsStep(
            Path.Combine(MakeTempRoot(), "settings.json"),
            alias: "axel",
            configPath: @"%APPDATA%\EstudioSocratico\config.json");

        var result = await step.DetectAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public async Task VerifyAsync_returns_ok_when_required_keys_match()
    {
        var root = MakeTempRoot();
        var settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "terminal.integrated.defaultProfile.windows": "PowerShell 7",
              "terminal.integrated.profiles.windows": {
                "PowerShell 7": { "path": "pwsh.exe" }
              },
              "estudioSocratico.alias": "axel",
              "estudioSocratico.configPath": "%APPDATA%\\EstudioSocratico\\config.json"
            }
            """);
        var step = new VsCodeSettingsStep(
            settingsPath,
            alias: "axel",
            configPath: @"%APPDATA%\EstudioSocratico\config.json");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task VerifyAsync_returns_missing_when_powershell7_profile_is_absent()
    {
        var root = MakeTempRoot();
        var settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "terminal.integrated.defaultProfile.windows": "PowerShell 7",
              "estudioSocratico.alias": "axel",
              "estudioSocratico.configPath": "%APPDATA%\\EstudioSocratico\\config.json"
            }
            """);
        var step = new VsCodeSettingsStep(
            settingsPath,
            alias: "axel",
            configPath: @"%APPDATA%\EstudioSocratico\config.json");

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("PowerShell 7", result.Message);
    }

    [Fact]
    public async Task UninstallAsync_removes_estudio_keys_and_preserves_other_settings()
    {
        var root = MakeTempRoot();
        var settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "editor.fontSize": 16,
              "terminal.integrated.defaultProfile.windows": "PowerShell 7",
              "terminal.integrated.profiles.windows": {
                "PowerShell 7": { "path": "pwsh.exe" }
              },
              "estudioSocratico.alias": "axel",
              "estudioSocratico.configPath": "%APPDATA%\\EstudioSocratico\\config.json"
            }
            """);
        var step = new VsCodeSettingsStep(
            settingsPath,
            alias: "axel",
            configPath: @"%APPDATA%\EstudioSocratico\config.json");

        var result = await step.UninstallAsync(new SetupContext(new SetupOptions(SetupMode.Uninstall)), CancellationToken.None);

        Assert.True(result.Success);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        Assert.Equal(16, document.RootElement.GetProperty("editor.fontSize").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("estudioSocratico.alias", out _));
        Assert.False(document.RootElement.TryGetProperty("estudioSocratico.configPath", out _));
        Assert.Single(Directory.GetFiles(root, "settings.json.*.bak"));
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }
}
