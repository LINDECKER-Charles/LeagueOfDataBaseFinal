using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>A bounded <see cref="Channel{T}"/> of <see cref="RobotsOptions.QueueCapacity"/> records.</summary>
/// <remarks>
/// A full queue drops the new record rather than wait: nginx's post must never pay for a slow
/// database.
/// </remarks>
internal sealed class RobotJournalQueue(IOptions<RobotsOptions> options, RobotsMetrics metrics)
    : IRobotJournal
{
    private readonly Channel<JournalRecord> _channel = Channel.CreateBounded<JournalRecord>(
        new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public void Take(IReadOnlyCollection<JournalRecord> records, int unreadable)
    {
        ArgumentNullException.ThrowIfNull(records);
        metrics.RecordReceived(records.Count + unreadable);
        if (unreadable > 0)
        {
            metrics.RecordDropped(unreadable, RobotsMetrics.Unreadable);
        }

        var dropped = records.Count(record => !_channel.Writer.TryWrite(record));
        if (dropped > 0)
        {
            metrics.RecordDropped(dropped, RobotsMetrics.QueueFull);
        }
    }

    public ValueTask<bool> WaitAsync(CancellationToken cancellationToken) =>
        _channel.Reader.WaitToReadAsync(cancellationToken);

    /// <summary>The records queued now, a batch at most; empty when there are none.</summary>
    public IReadOnlyList<JournalRecord> TakeBatch()
    {
        var batch = new List<JournalRecord>();
        while (batch.Count < options.Value.BatchSize && _channel.Reader.TryRead(out var record))
        {
            batch.Add(record);
        }

        return batch;
    }
}
