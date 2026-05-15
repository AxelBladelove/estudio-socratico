using Estudio.Setup.Services;

namespace Estudio.Setup.Tests;

public class ProcessEnvironmentPathTests
{
    [Fact]
    public void Merge_prioritizes_fresh_user_path_entries_before_process_path()
    {
        var processPath = string.Join(Path.PathSeparator, @"C:\Windows\System32", @"C:\msys64\mingw64\bin");
        var userPath = string.Join(Path.PathSeparator, @"C:\msys64\ucrt64\bin", @"C:\Windows\System32");

        var merged = ProcessEnvironmentPath.Merge(processPath, userPath);

        Assert.Equal(
            new[] { @"C:\msys64\ucrt64\bin", @"C:\Windows\System32", @"C:\msys64\mingw64\bin" },
            merged.Split(Path.PathSeparator));
    }
}
