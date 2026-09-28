using System.Diagnostics.Metrics;

namespace LoDb.Api.Modules.PublicApi;

/// <summary>
/// Meter <c>LoDb.PublicApi</c>: the refusals of <c>/v1</c> by code, and the metering of
/// the billed requests, from the events it lost to its writes to <c>api_usage</c>.
/// </summary>
internal sealed class PublicApiMetrics
{
    public const string MeterName = "LoDb.PublicApi";
    public const string CodeTag = "code";

    private readonly Counter<long> _refusals;
    private readonly Counter<long> _dropped;
    private readonly Histogram<double> _flushDuration;
    private readonly Counter<long> _flushFailures;
    private long _pending;

    public PublicApiMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _refusals = meter.CreateCounter<long>(
            "lodb.publicapi.refusals",
            unit: "{request}",
            description: "Requests of /v1 refused before their handler, by error code.");
        _dropped = meter.CreateCounter<long>(
            "lodb.publicapi.usage.dropped",
            unit: "{request}",
            description: "Billed requests left uncounted: the metering buffer was full.");
        _flushDuration = meter.CreateHistogram<double>(
            "lodb.publicapi.usage.flush.duration",
            unit: "s",
            description: "Duration of the writes of the request counts to api_usage.");
        _flushFailures = meter.CreateCounter<long>(
            "lodb.publicapi.usage.flush.failures",
            unit: "{flush}",
            description: "Writes to api_usage that failed, their counts kept for the next.");
        meter.CreateObservableGauge(
            "lodb.publicapi.usage.pending",
            () => Volatile.Read(ref _pending),
            unit: "{request}",
            description: "Billed requests counted in memory, not in api_usage yet.");
    }

    public void RecordRefusal(string code) =>
        _refusals.Add(1, new KeyValuePair<string, object?>(CodeTag, code));

    public void RecordDropped() => _dropped.Add(1);

    public void RecordFlush(TimeSpan duration) => _flushDuration.Record(duration.TotalSeconds);

    public void RecordFlushFailure() => _flushFailures.Add(1);

    public void RecordPending(long pending) => Volatile.Write(ref _pending, pending);
}
