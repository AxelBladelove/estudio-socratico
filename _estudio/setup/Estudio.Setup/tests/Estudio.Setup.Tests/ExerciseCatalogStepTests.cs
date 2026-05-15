using Estudio.Setup.Core;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class ExerciseCatalogStepTests
{
    [Fact]
    public async Task VerifyAsync_succeeds_when_alejandro_catalog_contains_gist_backed_exercises()
    {
        var root = MakeTempRoot();
        await WriteCatalogAsync(
            root,
            """
            {
              "provider": "alejandro",
              "exercises": [
                {
                  "slug": "alejandro-imprimir-nombre",
                  "title": "Imprimir nombre",
                  "gistInstructionsUrl": "https://gist.githubusercontent.com/user/id/raw/instructions.md"
                }
              ]
            }
            """);
        var step = new ExerciseCatalogStep(root);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("1 ejercicios", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_returns_missing_when_catalog_file_is_absent()
    {
        var step = new ExerciseCatalogStep(MakeTempRoot());

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public async Task VerifyAsync_fails_when_catalog_has_no_exercises()
    {
        var root = MakeTempRoot();
        await WriteCatalogAsync(root, """{"provider":"alejandro","exercises":[]}""");
        var step = new ExerciseCatalogStep(root);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no contiene ejercicios", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_fails_when_exercise_is_missing_instruction_source()
    {
        var root = MakeTempRoot();
        await WriteCatalogAsync(
            root,
            """
            {
              "provider": "alejandro",
              "exercises": [
                { "slug": "alejandro-sin-fuente", "title": "Sin fuente" }
              ]
            }
            """);
        var step = new ExerciseCatalogStep(root);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("gistInstructionsUrl", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_fails_when_catalog_json_is_invalid()
    {
        var root = MakeTempRoot();
        await WriteCatalogAsync(root, "{ invalid json");
        var step = new ExerciseCatalogStep(root);

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("JSON valido", result.Message);
    }

    private static async Task WriteCatalogAsync(string root, string json)
    {
        var path = Path.Combine(root, "_estudio", "soporte", "exercism", "catalogs", "alejandro.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
    }

    private static string MakeTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "estudio-setup-tests", Guid.NewGuid().ToString("N"));
    }
}
