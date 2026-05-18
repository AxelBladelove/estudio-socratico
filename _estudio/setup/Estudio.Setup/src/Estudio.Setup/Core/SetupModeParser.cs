namespace Estudio.Setup.Core;

public static class SetupModeParser
{
    public static SetupOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new SetupOptions(SetupMode.Verify, TuiRequested: true);
        }

        SetupMode? mode = null;
        string? stateRoot = null;
        string? aliasOverride = null;
        string? workspaceRoot = null;
        var onlyStepIds = new List<string>();
        var helpRequested = false;
        var tuiRequested = false;
        var forceGitHubRelogin = false;
        var jsonProgressRequested = false;
        var engine = SetupExecutionEngine.Legacy;
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            SplitInlineOption(arg, out var optionName, out var inlineValue);
            var normalized = Normalize(optionName);
            if (normalized is "help" or "h" or "?")
            {
                helpRequested = true;
                continue;
            }

            if (normalized == "state-root")
            {
                stateRoot = ReadOptionValue(args, ref index, inlineValue, "--state-root");
                continue;
            }

            if (normalized == "alias")
            {
                aliasOverride = ReadOptionValue(args, ref index, inlineValue, "--alias");
                continue;
            }

            if (normalized is "workspace-root" or "install-root")
            {
                workspaceRoot = ReadOptionValue(args, ref index, inlineValue, "--workspace-root");
                continue;
            }

            if (normalized is "tui" or "visual")
            {
                tuiRequested = true;
                continue;
            }

            if (normalized is "events-json" or "json-progress")
            {
                jsonProgressRequested = true;
                continue;
            }

            if (normalized == "desired-state")
            {
                engine = MergeEngine(engine, SetupExecutionEngine.DesiredState, arg);
                continue;
            }

            if (normalized == "engine")
            {
                var engineValue = Normalize(ReadOptionValue(args, ref index, inlineValue, "--engine"));
                var parsedEngine = engineValue switch
                {
                    "legacy" => SetupExecutionEngine.Legacy,
                    "desired-state" => SetupExecutionEngine.DesiredState,
                    _ => throw new ArgumentException($"Engine no reconocido: {engineValue}", nameof(args)),
                };

                engine = MergeEngine(engine, parsedEngine, arg);
                continue;
            }

            if (normalized is "change-github" or "cambiar-github" or "github-relogin" or "relogin-github")
            {
                forceGitHubRelogin = true;
                continue;
            }

            if (normalized == "only")
            {
                onlyStepIds.Add(ReadOptionValue(args, ref index, inlineValue, "--only"));
                continue;
            }

            var parsed = normalized switch
            {
                "" => (SetupMode?)null,
                "install" or "instalar" or "reconfigurar" => SetupMode.Install,
                "update" or "actualizar" => SetupMode.Update,
                "reinstall" or "reinstalar" => SetupMode.Reinstall,
                "repair" or "reparar" => SetupMode.Repair,
                "uninstall" or "desinstalar" => SetupMode.Uninstall,
                "verify" or "verificar" or "solo-verificar" or "soloverificar" => SetupMode.Verify,
                "package" or "pack" or "empaquetar" or "release" => SetupMode.Package,
                _ => throw new ArgumentException($"Argumento de modo no reconocido: {arg}", nameof(args)),
            };

            if (parsed is null)
            {
                continue;
            }

            if (mode is not null && mode != parsed)
            {
                throw new ArgumentException($"Se recibieron modos incompatibles: {mode} y {parsed}.", nameof(args));
            }

            mode = parsed;
        }

        return new SetupOptions(
            mode ?? SetupMode.Verify,
            stateRoot,
            aliasOverride,
            workspaceRoot,
            helpRequested,
            onlyStepIds.Count == 0 ? null : onlyStepIds.ToArray(),
            tuiRequested,
            forceGitHubRelogin,
            jsonProgressRequested,
            engine);
    }

    private static SetupExecutionEngine MergeEngine(SetupExecutionEngine current, SetupExecutionEngine requested, string arg)
    {
        if (current != requested && current != SetupExecutionEngine.Legacy)
        {
            throw new ArgumentException($"Se recibieron engines incompatibles alrededor de {arg}.", nameof(arg));
        }

        return requested;
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .TrimStart('-', '/', '\\')
            .Replace("_", "-", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static void SplitInlineOption(string value, out string optionName, out string? inlineValue)
    {
        var equalsIndex = value.IndexOf('=');
        if (equalsIndex < 0)
        {
            optionName = value;
            inlineValue = null;
            return;
        }

        optionName = value[..equalsIndex];
        inlineValue = value[(equalsIndex + 1)..];
    }

    private static string ReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string? inlineValue,
        string optionName)
    {
        if (inlineValue is not null)
        {
            if (inlineValue.Length == 0)
            {
                throw new ArgumentException($"{optionName} necesita un valor.", nameof(args));
            }

            return inlineValue;
        }

        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"{optionName} necesita un valor.", nameof(args));
        }

        return args[++index];
    }
}
