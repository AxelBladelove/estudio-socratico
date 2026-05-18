using System.Text.Json;
using Estudio.Setup.Profile;
using Estudio.Setup.Services;
using Estudio.Setup.State;

namespace Estudio.Setup.Core;

public static class SetupLocationResolver
{
    public static string ResolveWorkspaceRoot(
        SetupOptions options,
        string setupRoot,
        string currentDirectory,
        string studentAlias)
    {
        if (!string.IsNullOrWhiteSpace(options.WorkspaceRoot))
        {
            return Path.GetFullPath(options.WorkspaceRoot);
        }

        if (!LooksLikePackagedInstaller(setupRoot))
        {
            return LocalStudentProfile.FindWorkspaceRoot(currentDirectory);
        }

        var previousWorkspace = TryReadPreviousWorkspace(options.StateRoot);
        if (!string.IsNullOrWhiteSpace(previousWorkspace))
        {
            return previousWorkspace;
        }

        return ResolveDefaultWorkspaceRoot(studentAlias);
    }

    public static string ResolveDefaultWorkspaceRoot(string studentAlias, string? userProfile = null)
    {
        var root = string.IsNullOrWhiteSpace(userProfile)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : userProfile;
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("No se pudo resolver la carpeta de usuario para crear el workspace.");
        }

        return Path.Combine(root, $"Estudio Socratico-{studentAlias}");
    }

    public static bool LooksLikePackagedInstaller(string setupRoot)
    {
        return SetupPackageLayout.LooksLikePackagedInstaller(setupRoot);
    }

    private static string? TryReadPreviousWorkspace(string? explicitStateRoot)
    {
        var stateRoot = SetupPathDefaults.ResolveStateRoot(explicitStateRoot);
        var statePath = Path.Combine(stateRoot, "setup-state.json");
        if (!File.Exists(statePath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(statePath));
            if (!document.RootElement.TryGetProperty("workspace", out var workspaceElement))
            {
                return null;
            }

            var workspace = workspaceElement.GetString();
            return string.IsNullOrWhiteSpace(workspace) ? null : Path.GetFullPath(workspace);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}