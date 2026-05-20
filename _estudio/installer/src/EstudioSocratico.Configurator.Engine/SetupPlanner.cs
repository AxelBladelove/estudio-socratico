using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

/// <summary>
/// Generates a human-readable setup plan from diagnosis results.
/// The plan is shown to the user before any changes are applied.
/// </summary>
public sealed class SetupPlanner
{
    /// <summary>
    /// Generate a plan based on the current diagnosis state.
    /// </summary>
    public SetupPlan CreatePlan(
        IReadOnlyList<DependencyState> dependencies,
        AccountState? github,
        AccountState? exercism,
        bool workspaceValid,
        bool buildFlowValid)
    {
        var actions = new List<SetupAction>();

        // 1. System tools — install or repair missing/broken dependencies
        foreach (var dep in dependencies)
        {
            if (dep.Status == DependencyStatus.Ready || dep.Status == DependencyStatus.Skipped)
                continue;

            var isCritical = GlobalStateCalculator.IsCritical(dep.Id);

            switch (dep.Status)
            {
                case DependencyStatus.Missing:
                    actions.Add(new SetupAction
                    {
                        Id = $"install-{dep.Id.ToString().ToLowerInvariant()}",
                        Title = $"Instalar {dep.DisplayName}",
                        Description = $"{dep.DisplayName} no se encontró en el sistema.",
                        Category = ActionCategory.SystemTools,
                        Severity = isCritical ? ActionSeverity.Critical : ActionSeverity.Optional,
                        RequiresAdmin = dep.RequiresElevation || dep.Id == DependencyId.Msys2,
                        CanSkip = !isCritical,
                        DependencyId = dep.Id,
                        InternalCommand = "ensure-dependency",
                        EvidenceExpected = $"{dep.DisplayName} --version devuelve versión válida."
                    });
                    break;

                case DependencyStatus.Broken:
                    actions.Add(new SetupAction
                    {
                        Id = $"repair-{dep.Id.ToString().ToLowerInvariant()}",
                        Title = $"Reparar {dep.DisplayName}",
                        Description = dep.Error?.Description ?? $"{dep.DisplayName} existe pero no funciona correctamente.",
                        Category = ActionCategory.SystemTools,
                        Severity = isCritical ? ActionSeverity.Critical : ActionSeverity.Recommended,
                        RequiresAdmin = dep.RequiresElevation,
                        CanSkip = !isCritical,
                        DependencyId = dep.Id,
                        InternalCommand = "repair-dependency",
                        EvidenceExpected = $"{dep.DisplayName} responde correctamente.",
                        RollbackNotes = dep.Error?.RecommendedAction
                    });
                    break;

                case DependencyStatus.Outdated:
                    actions.Add(new SetupAction
                    {
                        Id = $"update-{dep.Id.ToString().ToLowerInvariant()}",
                        Title = $"Actualizar {dep.DisplayName}",
                        Description = $"{dep.DisplayName} {dep.Version} está desactualizado.",
                        Category = ActionCategory.SystemTools,
                        Severity = isCritical ? ActionSeverity.Recommended : ActionSeverity.Optional,
                        CanSkip = true,
                        DependencyId = dep.Id,
                        InternalCommand = "ensure-dependency",
                        EvidenceExpected = $"{dep.DisplayName} versión actualizada."
                    });
                    break;
            }
        }

        // 2. Authentication — GitHub
        if (github is null || !github.Configured)
        {
            actions.Add(new SetupAction
            {
                Id = "auth-github",
                Title = "Iniciar sesión en GitHub",
                Description = "Necesitamos conectar tu cuenta de GitHub para preparar el fork y workspace.",
                Category = ActionCategory.Authentication,
                Severity = ActionSeverity.Critical,
                RequiresUser = true,
                CanSkip = false,
                InternalCommand = "configure-github",
                EvidenceExpected = "gh auth status confirma sesión activa.",
                DependsOn = HasAction(actions, "install-githubcli") ? ["install-githubcli"] : []
            });
        }

        // 3. Authentication — Exercism
        if (exercism is null || !exercism.Configured)
        {
            actions.Add(new SetupAction
            {
                Id = "auth-exercism",
                Title = "Configurar Exercism",
                Description = "Necesitamos tu token de Exercism para conectar los ejercicios.",
                Category = ActionCategory.Authentication,
                Severity = ActionSeverity.Critical,
                RequiresUser = true,
                CanSkip = false,
                InternalCommand = "configure-exercism",
                EvidenceExpected = "exercism configure valida el token.",
                DependsOn = HasAction(actions, "install-exercismcli") ? ["install-exercismcli"] : []
            });
        }

        // 4. Workspace
        if (!workspaceValid)
        {
            actions.Add(new SetupAction
            {
                Id = "setup-workspace",
                Title = "Preparar workspace",
                Description = "Configurar la carpeta de estudio con fork, remotes y estructura.",
                Category = ActionCategory.Workspace,
                Severity = ActionSeverity.Critical,
                CanSkip = false,
                InternalCommand = "configure-workspace",
                EvidenceExpected = "Workspace existe con .git, remotes configurados.",
                DependsOn = HasAction(actions, "auth-github") ? ["auth-github"] : []
            });
        }

        // 5. VS Code setup
        var vscodeState = dependencies.FirstOrDefault(d => d.Id == DependencyId.VSCode);
        if (vscodeState?.Status == DependencyStatus.Ready && !buildFlowValid)
        {
            actions.Add(new SetupAction
            {
                Id = "setup-vscode",
                Title = "Configurar VS Code",
                Description = "Instalar extensiones, aplicar perfil y validar flujo F9.",
                Category = ActionCategory.Workspace,
                Severity = ActionSeverity.Critical,
                CanSkip = false,
                InternalCommand = "configure-vscode",
                EvidenceExpected = "Compilación de prueba con F9/GCC exitosa.",
                DependsOn = HasAction(actions, "setup-workspace") ? ["setup-workspace"] : []
            });
        }

        // 6. Final validation
        actions.Add(new SetupAction
        {
            Id = "validate-environment",
            Title = "Validación final",
            Description = "Ejecutar pruebas de verificación del entorno completo.",
            Category = ActionCategory.Validation,
            Severity = ActionSeverity.Critical,
            CanSkip = false,
            InternalCommand = "run-smoke-test",
            EvidenceExpected = "Todos los componentes responden y la compilación funciona."
        });

        return new SetupPlan
        {
            Actions = actions
        };
    }

    /// <summary>
    /// Convert dependency states to UI-friendly resource states.
    /// </summary>
    public IReadOnlyList<ResourceState> ToResourceStates(
        IReadOnlyList<DependencyState> dependencies,
        AccountState? github,
        AccountState? exercism)
    {
        var resources = new List<ResourceState>();

        foreach (var dep in dependencies)
        {
            var status = GlobalStateCalculator.ToResourceStatus(dep.Status);
            var isCritical = GlobalStateCalculator.IsCritical(dep.Id);

            resources.Add(new ResourceState
            {
                Id = dep.Id.ToString().ToLowerInvariant(),
                DisplayName = dep.DisplayName,
                Status = status,
                Category = GetCategory(dep.Id),
                IsCritical = isCritical,
                Version = dep.Version,
                Path = dep.Path,
                HumanStatus = GlobalStateCalculator.GetHumanStatus(status),
                HumanDescription = GetHumanDescription(dep),
                ActionLabel = GetActionLabel(dep),
                ActionId = GetActionId(dep),
                Error = dep.Error
            });
        }

        // Add auth resources
        resources.Add(new ResourceState
        {
            Id = "github-auth",
            DisplayName = "Cuenta GitHub",
            Status = github?.Configured == true ? ResourceStatus.Ready : ResourceStatus.NeedsAuth,
            Category = "auth",
            IsCritical = true,
            HumanStatus = github?.Configured == true ? $"Conectado como {github.UserName}" : "Necesita iniciar sesión",
            ActionLabel = github?.Configured == true ? "Cambiar cuenta" : "Iniciar sesión",
            ActionId = github?.Configured == true ? "change-github" : "auth-github"
        });

        resources.Add(new ResourceState
        {
            Id = "exercism-auth",
            DisplayName = "Cuenta Exercism",
            Status = exercism?.Configured == true ? ResourceStatus.Ready : ResourceStatus.NeedsAuth,
            Category = "auth",
            IsCritical = true,
            HumanStatus = exercism?.Configured == true ? "Configurado" : "Necesita configurar token",
            ActionLabel = exercism?.Configured == true ? null : "Configurar",
            ActionId = exercism?.Configured == true ? null : "auth-exercism"
        });

        return resources;
    }

    private static string GetCategory(DependencyId id) => id switch
    {
        DependencyId.Winget or DependencyId.NodeJs or DependencyId.Python => "optional",
        DependencyId.Git or DependencyId.GitHubCli or DependencyId.ExercismCli => "tools",
        DependencyId.VSCode => "editor",
        DependencyId.Msys2 or DependencyId.Gcc or DependencyId.Make => "compiler",
        _ => "other"
    };

    private static string? GetHumanDescription(DependencyState dep) => dep.Status switch
    {
        DependencyStatus.Ready => dep.Version is not null ? $"Versión {dep.Version}" : null,
        DependencyStatus.Missing => $"No se encontró {dep.DisplayName} en el sistema.",
        DependencyStatus.Broken => dep.Error?.Description ?? $"{dep.DisplayName} no funciona correctamente.",
        DependencyStatus.Outdated => $"Versión {dep.Version} — se recomienda actualizar.",
        _ => null
    };

    private static string? GetActionLabel(DependencyState dep) => dep.Status switch
    {
        DependencyStatus.Missing => "Instalar",
        DependencyStatus.Broken => "Reparar",
        DependencyStatus.Outdated => "Actualizar",
        _ => null
    };

    private static string? GetActionId(DependencyState dep) => dep.Status switch
    {
        DependencyStatus.Missing => $"install-{dep.Id.ToString().ToLowerInvariant()}",
        DependencyStatus.Broken => $"repair-{dep.Id.ToString().ToLowerInvariant()}",
        DependencyStatus.Outdated => $"update-{dep.Id.ToString().ToLowerInvariant()}",
        _ => null
    };

    private static bool HasAction(List<SetupAction> actions, string id) =>
        actions.Any(a => a.Id == id);
}
