using Estudio.Setup.Core;
using Estudio.Setup.Tui;

try
{
    var host = new SetupApplicationHost();
    var options = SetupModeParser.Parse(args);
    if (options.HelpRequested)
    {
        Console.WriteLine(SetupHelp.Text);
        return 0;
    }

    var currentDirectory = Directory.GetCurrentDirectory();
    if (host.ShouldRunPackage(options))
    {
        var bootstrapRoot = Estudio.Setup.Profile.LocalStudentProfile.FindWorkspaceRoot(currentDirectory);
        var package = await host.CreatePackageAsync(
            bootstrapRoot,
            CancellationToken.None);
        Console.WriteLine("Estudio.Setup 2.0 package");
        Console.WriteLine($"Carpeta: {package.PackageDirectory}");
        Console.WriteLine($"ZIP: {package.ZipPath}");
        Console.WriteLine($"Manifest: {package.ManifestPath}");
        return 0;
    }

    var launchContext = host.CreateLaunchContext(options, currentDirectory);
    if (host.DesiredStateNeedsVisualHost(options))
    {
        throw new InvalidOperationException("El engine desired-state ahora se usa desde la UI Windows. Ejecuta el instalador visual o usa --events-json para integraciones." );
    }

    if (host.ShouldRunTerminalGui(options))
    {
        return await TerminalGuiSetupApp.RunAsync(
            options,
            launchContext.WorkspaceRoot,
            launchContext.StudentAlias,
            launchContext.CommandRunner,
            CancellationToken.None);
    }

    var artifacts = await host.RunAsync(
        launchContext,
        options.JsonProgressRequested ? Console.Out : null,
        CancellationToken.None);

    if (options.JsonProgressRequested)
    {
        return artifacts.Success ? 0 : 1;
    }

    await SetupConsolePresenter.WriteAsync(Console.Out, options, artifacts, CancellationToken.None);

    return artifacts.Success ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
