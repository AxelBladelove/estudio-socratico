using System.Text.Json;
using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
var engine = new ConfiguratorEngine();

switch (command)
{
    case "scan":
        Console.WriteLine(JsonSerializer.Serialize(await engine.ScanAsync(), JsonDefaults.Options));
        return 0;

    case "diagnose":
        Console.WriteLine(JsonSerializer.Serialize(
            await engine.RunAsync(new SetupRequest { Mode = SetupMode.Diagnostics }),
            JsonDefaults.Options));
        return 0;

    case "install":
    case "repair":
    case "reinstall":
    case "uninstall":
        var request = new SetupRequest
        {
            Mode = command switch
            {
                "repair" => SetupMode.Repair,
                "reinstall" => SetupMode.Reinstall,
                "uninstall" => SetupMode.Uninstall,
                _ => SetupMode.Install
            },
            WorkspacePath = GetOption(args, "--workspace"),
            LocalAlias = GetOption(args, "--alias"),
            ExercismToken = GetOption(args, "--exercism-token"),
            SkipExercism = args.Contains("--skip-exercism"),
            SkipGitHubLogin = args.Contains("--skip-github"),
            AllowAggressiveCleanup = args.Contains("--aggressive")
        };
        var summary = await engine.RunAsync(request);
        Console.WriteLine(JsonSerializer.Serialize(summary, JsonDefaults.Options));
        return summary.Succeeded ? 0 : 1;

    default:
        Console.WriteLine("""
        Estudio Socratico Configurador CLI

        Commands:
          scan
          diagnose
          install [--workspace path] [--alias slug] [--exercism-token token] [--skip-github] [--skip-exercism]
          repair [--workspace path] [--alias slug] [--skip-github] [--skip-exercism]
          reinstall [--workspace path] [--alias slug] [--skip-github] [--skip-exercism]
          uninstall [--aggressive]
        """);
        return 0;
}

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}
