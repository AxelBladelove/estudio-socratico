using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public class UserPathStepTests
{
    [Fact]
    public async Task VerifyAsync_succeeds_when_required_entries_are_in_user_path()
    {
        var environment = new FakeUserEnvironment(@"C:\Tools;C:\msys64\ucrt64\bin");
        var step = new UserPathStep(environment, new[] { @"C:\msys64\ucrt64\bin" });

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task VerifyAsync_returns_missing_when_required_entry_is_absent()
    {
        var environment = new FakeUserEnvironment(@"C:\Tools");
        var step = new UserPathStep(environment, new[] { @"C:\msys64\ucrt64\bin" });

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains(@"C:\msys64\ucrt64\bin", result.Message);
    }

    [Fact]
    public async Task RepairAsync_moves_required_entries_to_front_without_duplicating_existing_ones()
    {
        var environment = new FakeUserEnvironment(@"C:\Tools;C:\msys64\ucrt64\bin");
        var step = new UserPathStep(environment, new[] { @"C:\msys64\ucrt64\bin", @"C:\NewTool" });

        var result = await step.RepairAsync(new SetupContext(new SetupOptions(SetupMode.Repair)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(@"C:\msys64\ucrt64\bin;C:\NewTool;C:\Tools", environment.UserPath);
    }

    [Fact]
    public async Task UninstallAsync_removes_required_entries_from_user_path()
    {
        var environment = new FakeUserEnvironment(@"C:\Tools;C:\msys64\ucrt64\bin;C:\Other");
        var step = new UserPathStep(environment, new[] { @"C:\msys64\ucrt64\bin" });

        var result = await step.UninstallAsync(new SetupContext(new SetupOptions(SetupMode.Uninstall)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(@"C:\Tools;C:\Other", environment.UserPath);
    }

    private sealed class FakeUserEnvironment : IUserEnvironment
    {
        public FakeUserEnvironment(string? userPath)
        {
            UserPath = userPath;
        }

        public string? UserPath { get; private set; }

        public string? GetUserVariable(string name)
        {
            return string.Equals(name, "PATH", StringComparison.OrdinalIgnoreCase) ? UserPath : null;
        }

        public void SetUserVariable(string name, string value)
        {
            if (string.Equals(name, "PATH", StringComparison.OrdinalIgnoreCase))
            {
                UserPath = value;
            }
        }
    }
}
