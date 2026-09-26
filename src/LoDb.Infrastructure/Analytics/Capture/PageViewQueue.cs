using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// A bounded <see cref="Channel{T}"/> of <see cref="AnalyticsOptions.QueueCapacity"/> views.
/// </summary>
/// <remarks>
/// A full queue drops the new view rather than wait: the request it came with must never
/// pay for a slow database.
/// </remarks>
internal sealed class PageViewQueue(IOptions<AnalyticsOptions> options, AnalyticsMetrics metrics)
    : IPageViewCapture
{
    private readonly Channel<CapturedView> _channel = Channel.CreateBounded<CapturedView>(
        new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    /// <summary>Where the views of this queue are counted.</summary>
    public AnalyticsMetrics Metrics => metrics;

    public bool TryCapture(CapturedView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!_channel.Writer.TryWrite(view))
        {
            metrics.RecordDropped(1, AnalyticsMetrics.QueueFull);
            return false;
        }

        metrics.RecordQueued(view.Origin);
        return true;
    }

    public ValueTask<bool> WaitAsync(CancellationToken cancellationToken) =>
        _channel.Reader.WaitToReadAsync(cancellationToken);

    /// <summary>The views queued now, a batch at most; empty when there are none.</summary>
    public IReadOnlyList<CapturedView> TakeBatch()
    {
        var batch = new List<CapturedView>();
        while (batch.Count < options.Value.BatchSize && _channel.Reader.TryRead(out var view))
        {
            batch.Add(view);
        }

        return batch;
    }
}
