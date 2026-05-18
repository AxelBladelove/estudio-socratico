using Estudio.Setup.Core;

namespace Estudio.Setup.Tests;

public class SetupModeParserTests
{
    [Theory]
    [InlineData(new string[] { }, SetupMode.Verify)]
    [InlineData(new[] { "install" }, SetupMode.Install)]
    [InlineData(new[] { "--install" }, SetupMode.Install)]
    [InlineData(new[] { "-Actualizar" }, SetupMode.Update)]
    [InlineData(new[] { "--update" }, SetupMode.Update)]
    [InlineData(new[] { "reinstall" }, SetupMode.Reinstall)]
    [InlineData(new[] { "reinstalar" }, SetupMode.Reinstall)]
    [InlineData(new[] { "repair" }, SetupMode.Repair)]
    [InlineData(new[] { "-Reparar" }, SetupMode.Repair)]
    [InlineData(new[] { "uninstall" }, SetupMode.Uninstall)]
    [InlineData(new[] { "desinstalar" }, SetupMode.Uninstall)]
    [InlineData(new[] { "verify" }, SetupMode.Verify)]
    [InlineData(new[] { "-SoloVerificar" }, SetupMode.Verify)]
    [InlineData(new[] { "package" }, SetupMode.Package)]
    [InlineData(new[] { "empaquetar" }, SetupMode.Package)]
    public void Parse_recognizes_legacy_and_new_mode_arguments(string[] args, SetupMode expected)
    {
        var options = SetupModeParser.Parse(args);

        Assert.Equal(expected, options.Mode);
    }

    [Fact]
    public void Parse_defaults_to_tui_verify_when_no_arguments_are_provided()
    {
        var options = SetupModeParser.Parse(Array.Empty<string>());

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.Equal(SetupExecutionEngine.Legacy, options.Engine);
        Assert.True(options.TuiRequested);
    }

    [Fact]
    public void Parse_defaults_to_verify_when_only_visual_flag_is_provided()
    {
        var options = SetupModeParser.Parse(new[] { "--tui" });

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.True(options.TuiRequested);
    }

    [Fact]
    public void Parse_rejects_unknown_mode_arguments()
    {
        var error = Assert.Throws<ArgumentException>(() => SetupModeParser.Parse(new[] { "--surprise" }));

        Assert.Contains("--surprise", error.Message);
    }

    [Fact]
    public void Parse_accepts_state_root_option()
    {
        var options = SetupModeParser.Parse(new[] { "verify", "--state-root", "C:\\Temp\\EstudioState" });

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.Equal("C:\\Temp\\EstudioState", options.StateRoot);
    }

    [Fact]
    public void Parse_accepts_state_root_option_with_equals()
    {
        var options = SetupModeParser.Parse(new[] { "verify", "--state-root=C:\\Temp\\EstudioState" });

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.Equal("C:\\Temp\\EstudioState", options.StateRoot);
    }

    [Fact]
    public void Parse_accepts_alias_override_option()
    {
        var options = SetupModeParser.Parse(new[] { "verify", "--alias", "axel_02" });

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.Equal("axel_02", options.AliasOverride);
    }

    [Fact]
    public void Parse_accepts_alias_override_option_with_equals()
    {
        var options = SetupModeParser.Parse(new[] { "verify", "--alias=axel_02" });

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.Equal("axel_02", options.AliasOverride);
    }

    [Fact]
    public void Parse_accepts_workspace_root_option()
    {
        var options = SetupModeParser.Parse(new[] { "install", "--workspace-root", "C:\\Users\\Axel\\Estudio Socratico-axel" });

        Assert.Equal(SetupMode.Install, options.Mode);
        Assert.Equal("C:\\Users\\Axel\\Estudio Socratico-axel", options.WorkspaceRoot);
    }

    [Fact]
    public void Parse_accepts_workspace_root_option_with_equals()
    {
        var options = SetupModeParser.Parse(new[] { "install", "--workspace-root=C:\\Users\\Axel\\Estudio Socratico-axel" });

        Assert.Equal(SetupMode.Install, options.Mode);
        Assert.Equal("C:\\Users\\Axel\\Estudio Socratico-axel", options.WorkspaceRoot);
    }

    [Theory]
    [InlineData("--change-github")]
    [InlineData("--cambiar-github")]
    [InlineData("--github-relogin")]
    public void Parse_accepts_github_relogin_flag(string arg)
    {
        var options = SetupModeParser.Parse(new[] { "update", arg });

        Assert.Equal(SetupMode.Update, options.Mode);
        Assert.True(options.ForceGitHubRelogin);
    }

    [Fact]
    public void Parse_accepts_only_step_filter()
    {
        var options = SetupModeParser.Parse(new[] { "repair", "--only", "vscode-settings", "--only=git-remotes" });

        Assert.Equal(SetupMode.Repair, options.Mode);
        Assert.Equal(new[] { "vscode-settings", "git-remotes" }, options.OnlyStepIds);
    }

    [Theory]
    [InlineData("--tui")]
    [InlineData("--visual")]
    public void Parse_accepts_tui_flag(string arg)
    {
        var options = SetupModeParser.Parse(new[] { "verify", arg });

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.True(options.TuiRequested);
    }

    [Fact]
    public void Parse_accepts_json_progress_flag_for_textual_frontend()
    {
        var options = SetupModeParser.Parse(new[] { "verify", "--events-json" });

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.True(options.JsonProgressRequested);
    }

    [Theory]
    [InlineData("--desired-state")]
    [InlineData("--engine", "desired-state")]
    public void Parse_accepts_desired_state_engine_flags(params string[] args)
    {
        var options = SetupModeParser.Parse(new[] { "verify" }.Concat(args).ToArray());

        Assert.Equal(SetupMode.Verify, options.Mode);
        Assert.Equal(SetupExecutionEngine.DesiredState, options.Engine);
    }

    [Fact]
    public void Parse_accepts_explicit_legacy_engine_flag()
    {
        var options = SetupModeParser.Parse(new[] { "verify", "--engine", "legacy" });

        Assert.Equal(SetupExecutionEngine.Legacy, options.Engine);
    }

    [Fact]
    public void Parse_rejects_missing_alias_value()
    {
        var error = Assert.Throws<ArgumentException>(() => SetupModeParser.Parse(new[] { "verify", "--alias" }));

        Assert.Contains("--alias", error.Message);
    }

    [Fact]
    public void Parse_rejects_missing_only_value()
    {
        var error = Assert.Throws<ArgumentException>(() => SetupModeParser.Parse(new[] { "verify", "--only" }));

        Assert.Contains("--only", error.Message);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Parse_accepts_help_flags(string arg)
    {
        var options = SetupModeParser.Parse(new[] { arg });

        Assert.True(options.HelpRequested);
    }

    [Fact]
    public void SetupHelp_mentions_modes_and_supported_options()
    {
        var text = SetupHelp.Text;

        Assert.Contains("install", text);
        Assert.Contains("update", text);
        Assert.Contains("reinstall", text);
        Assert.Contains("uninstall", text);
        Assert.Contains("package", text);
        Assert.Contains("--alias", text);
        Assert.Contains("--change-github", text);
        Assert.Contains("--workspace-root", text);
        Assert.Contains("--only", text);
        Assert.Contains("--tui", text);
        Assert.Contains("--events-json", text);
        Assert.Contains("--state-root", text);
        Assert.Contains("--desired-state", text);
        Assert.Contains("--engine", text);
    }
}
