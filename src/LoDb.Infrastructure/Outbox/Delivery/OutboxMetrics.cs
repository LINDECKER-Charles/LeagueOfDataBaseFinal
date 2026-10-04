using System.Diagnostics.Metrics;

namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// Meter <c>LoDb.Outbox</c>: outcome of every message, duration and failures of the
/// batches, and the depth of the queue as the last batch of this instance saw it.
/// </summary>
internal sealed class OutboxMetrics
{
    public const string MeterName = "LoDb.Outbox";
    public const string OutcomeTag = "outcome";

    private readonly Counter<long> _messages;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _failures;
    private long _pending;

    public OutboxMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _messages = meter.CreateCounter<long>(
            "lodb.outbox.messages",
            unit: "{message}",
            description: "Delivery attempts, by outcome: sent, retried or dead.");
        _duration = meter.CreateHistogram<double>(
            "lodb.outbox.batch.duration",
            unit: "s",
            description: "Duration of the batches that took at least one message.");
        _failures = meter.CreateCounter<long>(
            "lodb.outbox.batch.failures",
            unit: "{batch}",
            description: "Batches cut short by a failure of the database.");
        meter.CreateObservableGauge(
            "lodb.outbox.pending",
            () => Volatile.Read(ref _pending),
            unit: "{message}",
            description: "Messages waiting for a first or next attempt.");
    }

    public void RecordMessages(string outcome, int count)
    {
        if (count > 0)
        {
            _messages.Add(count, new KeyValuePair<string, object?>(OutcomeTag, outcome));
        }
    }

    public void RecordBatch(TimeSpan duration) => _duration.Record(duration.TotalSeconds);

    public void RecordPending(long pending) => Volatile.Write(ref _pending, pending);

    public void RecordFailure() => _failures.Add(1);
}
