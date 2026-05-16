using Estudio.Setup.Core;
using Estudio.Setup.Services;
using Estudio.Setup.Steps;

namespace Estudio.Setup.Tests;

public sealed class ExercismCTrackStepTests
{
    [Fact]
    public async Task InstallAsync_opens_token_page_when_token_is_missing()
    {
        var openedUrls = new List<string>();
        var runner = new QueueCommandRunner(CommandResult.Success("Token: (-t, --token)"));
        var step = new ExercismCTrackStep(
            runner,
            workspacePath: @"C:\Users\Ada\Exercism",
            tokenProvider: () => string.Empty,
            openUrl: openedUrls.Add);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains(ExercismCTrackStep.TokenUrl, result.Message);
        Assert.Equal(new[] { ExercismCTrackStep.TokenUrl }, openedUrls);
        Assert.Equal(("exercism", "configure --show"), runner.Calls.Single());
    }

    [Fact]
    public async Task InstallAsync_configures_token_prepares_and_downloads_c_hello_world()
    {
        const string token = "sample-token-for-tests";
        var runner = new QueueCommandRunner(
            CommandResult.Success("Token: (-t, --token)"),
            CommandResult.Success("configured"),
            CommandResult.Success("prepared"),
            CommandResult.Success("Downloaded to C:\\Users\\Ada\\Exercism\\c\\hello-world"));
        var step = new ExercismCTrackStep(
            runner,
            workspacePath: @"C:\Users\Ada\Exercism",
            tokenProvider: () => token,
            openUrl: _ => { });

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("exercism", "configure --show"), runner.Calls[0]);
        Assert.Equal(("exercism", $"configure --token {token} --workspace C:\\Users\\Ada\\Exercism"), runner.Calls[1]);
        Assert.Equal(("exercism", "prepare"), runner.Calls[2]);
        Assert.Equal(("exercism", "download --track c --exercise hello-world"), runner.Calls[3]);
        Assert.DoesNotContain(token, result.Message);
    }

    [Fact]
    public async Task VerifyAsync_accepts_existing_hello_world_directory_as_c_track_ready()
    {
        var runner = new QueueCommandRunner(
            CommandResult.Success("Token: (-t, --token) configured-token"),
            CommandResult.Failure(1, string.Empty, "directory 'C:\\Users\\Ada\\Exercism\\c\\hello-world' already exists"));
        var step = new ExercismCTrackStep(
            runner,
            workspacePath: @"C:\Users\Ada\Exercism",
            tokenProvider: () => string.Empty,
            openUrl: _ => { });

        var result = await step.VerifyAsync(new SetupContext(new SetupOptions(SetupMode.Verify)), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(("exercism", "configure --show"), runner.Calls[0]);
        Assert.Equal(("exercism", "download --track c --exercise hello-world"), runner.Calls[1]);
    }

    [Fact]
    public async Task InstallAsync_opens_c_track_page_when_account_has_not_joined_track()
    {
        var openedUrls = new List<string>();
        var runner = new QueueCommandRunner(
            CommandResult.Success("Token: (-t, --token) configured-token"),
            CommandResult.Success("prepared"),
            CommandResult.Failure(1, string.Empty, "track_not_joined"));
        var step = new ExercismCTrackStep(
            runner,
            workspacePath: @"C:\Users\Ada\Exercism",
            tokenProvider: () => string.Empty,
            openUrl: openedUrls.Add);

        var result = await step.InstallAsync(new SetupContext(new SetupOptions(SetupMode.Install)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsMissing);
        Assert.Contains("track C", result.Message);
        Assert.Equal(new[] { ExercismCTrackStep.CTrackUrl }, openedUrls);
    }

    private sealed class QueueCommandRunner : ICommandRunner
    {
        private readonly Queue<CommandResult> _results;
        private readonly List<(string FileName, string Arguments)> _calls = new();

        public QueueCommandRunner(params CommandResult[] results)
        {
            _results = new Queue<CommandResult>(results);
        }

        public IReadOnlyList<(string FileName, string Arguments)> Calls => _calls;

        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            _calls.Add((fileName, arguments));
            return Task.FromResult(_results.Dequeue());
        }
    }
}
