using System.Threading.Channels;

namespace AltTextGen.Server.Services;

public sealed record AltTextBatchJobWorkItem(
    Guid JobId,
    IReadOnlyList<Guid> MediaKeys,
    string? Culture,
    string? Segment,
    bool Overwrite);

public interface IAltTextBatchJobQueue
{
    ValueTask QueueAsync(AltTextBatchJobWorkItem workItem, CancellationToken cancellationToken);
    IAsyncEnumerable<AltTextBatchJobWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class AltTextBatchJobQueue : IAltTextBatchJobQueue
{
    private readonly Channel<AltTextBatchJobWorkItem> _channel = Channel.CreateUnbounded<AltTextBatchJobWorkItem>();

    public ValueTask QueueAsync(AltTextBatchJobWorkItem workItem, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(workItem, cancellationToken);
    }

    public IAsyncEnumerable<AltTextBatchJobWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
