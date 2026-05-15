namespace Estudio.Setup.Steps;

public static class VsCodeCliPathResolver
{
    public static string ResolveCodeCommand()
    {
        return ResolveCodeCommand(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
    }

    public static string ResolveCodeCommand(string localAppData, string programFiles, string programFilesX86)
    {
        var candidates = new[]
        {
            Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd"),
            Path.Combine(programFiles, "Microsoft VS Code", "bin", "code.cmd"),
            Path.Combine(programFilesX86, "Microsoft VS Code", "bin", "code.cmd"),
        };

        return candidates.FirstOrDefault(File.Exists) ?? "code";
    }
}
