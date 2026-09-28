using System.Diagnostics.Metrics;
using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics;

/// <summary>
/// Meter <c>LoDb.Analytics</c>: the views queued by origin, written, and dropped by reason.
/// </summary>
/// <remarks>Counts only: no path, address or user agent ever becomes a tag.</remarks>
public sealed class AnalyticsMetrics
{
    public const string MeterName = "LoDb.Analytics";
    public const string OriginTag = "origin";
    public const string ReasonTag = "reason";

    /// <summary>The queue was full: the writer is behind or the database down.</summary>
    public const string QueueFull = "queue_full";

    /// <summary>The page of the view could not be resolved.</summary>
    public const string Unresolved = "unresolved";

    /// <summary>The batch of the view could not be written.</summary>
    public const string WriteFailed = "write_failed";

    private readonly Counter<long> _queued;
    private readonly Counter<long> _written;
    private readonly Counter<long> _dropped;

    public AnalyticsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _queued = meter.CreateCounter<long>(
            "lodb.analytics.views.queued",
            unit: "{view}",
            description: "Page views taken in by the capture endpoints.");
        _written = meter.CreateCounter<long>(
            "lodb.analytics.views.written",
            unit: "{view}",
            description: "Page views written to analytics_event.");
        _dropped = meter.CreateCounter<long>(
            "lodb.analytics.views.dropped",
            unit: "{view}",
            description: "Page views lost before analytics_event.");
    }

    public void RecordQueued(AnalyticsCaptureOrigin origin) =>
        _queued.Add(
            1,
            new KeyValuePair<string, object?>(OriginTag, AnalyticsColumns.ToText(origin)));

    public void RecordWritten(int views) => _written.Add(views);

    public void RecordDropped(int views, string reason) =>
        _dropped.Add(views, new KeyValuePair<string, object?>(ReasonTag, reason));
}
