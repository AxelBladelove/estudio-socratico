using Estudio.Setup.Core;
using Estudio.Setup.Profile;
using Estudio.Setup.Release;
using Estudio.Setup.Services;
using Estudio.Setup.Tui;

try
{
    var options = SetupModeParser.Parse(args);
    if (options.HelpRequested)
    {
        Console.WriteLine(SetupHelp.Text);
        return 0;
    }

    var workspaceRoot = LocalStudentProfile.FindWorkspaceRoot(Directory.GetCurrentDirectory());
    var commandRunner = new ProcessCommandRunner();
    if (options.Mode == SetupMode.Package)
    {
        var package = await new ReleasePackager(commandRunner).CreateAsync(
            ReleasePackager.ForWorkspace(workspaceRoot),
            CancellationToken.None);
        Console.WriteLine("Estudio.Setup 2.0 package");
        Console.WriteLine($"Carpeta: {package.PackageDirectory}");
        Console.WriteLine($"ZIP: {package.ZipPath}");
        Console.WriteLine($"Manifest: {package.ManifestPath}");
        return 0;
    }

    var studentAlias = options.AliasOverride ?? LocalStudentProfile.ResolveAlias(workspaceRoot);
    LocalStudentProfile.ValidateAlias(studentAlias);
    if (options.TuiRequested)
    {
        return await TerminalGuiSetupApp.RunAsync(
            options,
            workspaceRoot,
            studentAlias,
            commandRunner,
            CancellationToken.None);
    }

    var artifacts = await new SetupRunCoordinator().RunAndPersistAsync(
        options,
        workspaceRoot,
        studentAlias,
        commandRunner,
        NullSetupProgressSink.Instance,
        CancellationToken.None);

    Console.WriteLine("Estudio.Setup 2.0");
    Console.WriteLine($"Modo: {options.Mode}");
    Console.WriteLine($"Alias: {studentAlias}");
    Console.WriteLine($"Estado: {artifacts.StatePath}");
    Console.WriteLine($"Log: {artifacts.LogPath}");
    Console.WriteLine($"Reporte: {artifacts.ReportPath}");
    foreach (var step in artifacts.Report.Steps)
    {
        var marker = step.Result.IsWarning ? "ADVERTENCIA" : step.Result.Success ? "OK" : step.Result.IsMissing ? "FALTA" : "ERROR";
        Console.WriteLine($"{marker} {step.StepId}.{step.Phase}: {step.Result.Message}");
    }

    Console.WriteLine(artifacts.Report.Success ? "Resultado: OK" : "Resultado: ERROR");

    return artifacts.Report.Success ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
