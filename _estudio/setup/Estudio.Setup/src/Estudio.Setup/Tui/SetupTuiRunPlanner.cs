using Estudio.Setup.Core;

namespace Estudio.Setup.Tui;

public static class SetupTuiRunPlanner
{
    public static SetupOptions ForMode(SetupOptions baseline, SetupMode mode)
    {
        return baseline with
        {
            Mode = mode,
            OnlyStepIds = null,
            TuiRequested = true,
            ForceGitHubRelogin = false,
        };
    }

    public static SetupOptions ChangeGitHub(SetupOptions baseline)
    {
        return baseline with
        {
            Mode = SetupMode.Update,
            OnlyStepIds = null,
            TuiRequested = true,
            ForceGitHubRelogin = true,
        };
    }

    public static SetupOptions ChangeAlias(SetupOptions baseline, string alias)
    {
        return baseline with
        {
            Mode = SetupMode.Update,
            AliasOverride = alias,
            OnlyStepIds = null,
            TuiRequested = true,
            ForceGitHubRelogin = false,
        };
    }

    public static SetupOptions? RetryFailed(SetupOptions baseline, IReadOnlyList<string> failedStepIds)
    {
        if (failedStepIds.Count == 0)
        {
            return null;
        }

        return baseline with
        {
            Mode = SetupMode.Repair,
            OnlyStepIds = failedStepIds.ToArray(),
            TuiRequested = true,
            ForceGitHubRelogin = false,
        };
    }
}
