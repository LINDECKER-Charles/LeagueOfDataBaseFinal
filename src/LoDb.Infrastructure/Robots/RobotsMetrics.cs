using System.Diagnostics.Metrics;

namespace LoDb.Infrastructure.Robots;

/// <summary>
/// Meter <c>LoDb.Robots</c>: the records nginx reported, the hits written by detection, what
/// was dropped by reason, and the reverse DNS lookups by outcome.
/// </summary>
/// <remarks>Counts only: no address, path or user agent ever becomes a tag.</remarks>
public sealed class RobotsMetrics
{
    public const string MeterName = "LoDb.Robots";
    public const string ReasonTag = "reason";
    public const string DetectionTag = "detection";
    public const string OutcomeTag = "outcome";

    /// <summary>The queue was full: the writer is behind or the database down.</summary>
    public const string QueueFull = "queue_full";

    /// <summary>A line nginx sent could not be read.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>The batch of the hit could not be written.</summary>
    public const string WriteFailed = "write_failed";

    private readonly Counter<long> _received;
    private readonly Counter<long> _written;
    private readonly Counter<long> _dropped;
    private readonly Counter<long> _lookups;

    public RobotsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _received = meter.CreateCounter<long>(
            "lodb.robots.records.received",
            unit: "{record}",
            description: "Requests nginx reported to the robots journal.");
        _written = meter.CreateCounter<long>(
            "lodb.robots.hits.written",
            unit: "{hit}",
            description: "Hits of robots written to robot_hit.");
        _dropped = meter.CreateCounter<long>(
            "lodb.robots.records.dropped",
            unit: "{record}",
            description: "Records of the robots journal lost before robot_hit.");
        _lookups = meter.CreateCounter<long>(
            "lodb.robots.dns.lookups",
            unit: "{lookup}",
            description: "Reverse DNS lookups of the robots' addresses.");
    }

    public void RecordReceived(int records) => _received.Add(records);

    public void RecordWritten(int hits, string detection) =>
        _written.Add(hits, new KeyValuePair<string, object?>(DetectionTag, detection));

    public void RecordDropped(int records, string reason) =>
        _dropped.Add(records, new KeyValuePair<string, object?>(ReasonTag, reason));

    public void RecordLookup(string outcome) =>
        _lookups.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome));
}
