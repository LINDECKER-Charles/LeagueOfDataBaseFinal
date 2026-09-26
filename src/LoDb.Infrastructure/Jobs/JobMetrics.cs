using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// Meter <c>LoDb.Jobs</c>: duration, failures and last success of every periodic job, tagged
/// with the job's name.
/// </summary>
/// <remarks>
/// The last success is the one this instance saw: across instances, take the maximum.
/// </remarks>
public sealed class JobMetrics
{
    public const string MeterName = "LoDb.Jobs";
    public const string JobTag = "job";
    public const string OutcomeTag = "outcome";

    private readonly Histogram<double> _duration;
    private readonly Counter<long> _failures;
    private readonly ConcurrentDictionary<string, long> _lastSuccess = new(StringComparer.Ordinal);

    public JobMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _duration = meter.CreateHistogram<double>(
            "lodb.job.duration",
            unit: "s",
            description: "Duration of the runs of a periodic job.");
        _failures = meter.CreateCounter<long>(
            "lodb.job.failures",
            unit: "{run}",
            description: "Runs of a periodic job that failed.");
        meter.CreateObservableGauge(
            "lodb.job.last_success",
            ObserveLastSuccess,
            unit: "s",
            description: "Unix time of the last successful run of a periodic job.");
    }

    public void RecordSuccess(string job, TimeSpan duration, DateTimeOffset finishedAt)
    {
        _duration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(JobTag, job),
            new KeyValuePair<string, object?>(OutcomeTag, "succeeded"));
        _lastSuccess[job] = finishedAt.ToUnixTimeSeconds();
    }

    public void RecordFailure(string job, TimeSpan? duration)
    {
        if (duration is { } ran)
        {
            _duration.Record(
                ran.TotalSeconds,
                new KeyValuePair<string, object?>(JobTag, job),
                new KeyValuePair<string, object?>(OutcomeTag, "failed"));
        }

        _failures.Add(1, new KeyValuePair<string, object?>(JobTag, job));
    }

    private IEnumerable<Measurement<long>> ObserveLastSuccess() =>
        _lastSuccess.Select(static entry => new Measurement<long>(
            entry.Value,
            new KeyValuePair<string, object?>(JobTag, entry.Key)));
}
