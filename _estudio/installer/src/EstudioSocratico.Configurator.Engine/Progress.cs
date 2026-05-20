using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public interface IProgressSink
{
    Task ReportAsync(ProgressEvent progress, CancellationToken cancellationToken = default);
}

public sealed class NullProgressSink : IProgressSink
{
    public static NullProgressSink Instance { get; } = new();

    public Task ReportAsync(ProgressEvent progress, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class CompositeProgressSink(IEnumerable<IProgressSink> sinks) : IProgressSink
{
    private readonly IReadOnlyList<IProgressSink> _sinks = sinks.ToArray();

    public async Task ReportAsync(ProgressEvent progress, CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
        {
            await sink.ReportAsync(progress, cancellationToken).ConfigureAwait(false);
        }
    }
}
