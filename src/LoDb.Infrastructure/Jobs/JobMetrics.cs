using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// Meter <c>LoDb.Jobs</c>: period, duration, failures and last success of every periodic job,
/// tagged with the job's name.
/// </summary>
/// <remarks>
/// <para>
/// The tag is <c>task</c>, not <c>job</c>: Prometheus names the scrape target <c>job</c>, and a
/// series' own <c>job</c> label would be renamed <c>exported_job</c> at the first scrape.
/// </para>
/// <para>
/// The period is measured from the job's start, so that an alert can weigh the age of the
/// last success against it. The last success is the one this instance saw: across
/// instances, take the maximum.
/// </para>
/// </remarks>
public sealed class JobMetrics
{
    public const string MeterName = "LoDb.Jobs";
    public const string TaskTag = "task";
    public const string OutcomeTag = "outcome";

    private readonly Histogram<double> _duration;
    private readonly Counter<long> _failures;
    private readonly ConcurrentDictionary<string, double> _periods = new(StringComparer.Ordinal);
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
            "lodb.job.period",
            () => Observe(_periods),
            unit: "s",
            description: "Time between two runs of a periodic job.");
        meter.CreateObservableGauge(
            "lodb.job.last_success",
            () => Observe(_lastSuccess),
            unit: "s",
            description: "Unix time of the last successful run of a periodic job.");
    }

    /// <summary>Records the period of a job whose timer starts.</summary>
    public void RecordPeriod(string job, TimeSpan period) =>
        _periods[job] = period.TotalSeconds;

    public void RecordSuccess(string job, TimeSpan duration, DateTimeOffset finishedAt)
    {
        _duration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(TaskTag, job),
            new KeyValuePair<string, object?>(OutcomeTag, "succeeded"));
        _lastSuccess[job] = finishedAt.ToUnixTimeSeconds();
    }

    public void RecordFailure(string job, TimeSpan? duration)
    {
        if (duration is { } ran)
        {
            _duration.Record(
                ran.TotalSeconds,
                new KeyValuePair<string, object?>(TaskTag, job),
                new KeyValuePair<string, object?>(OutcomeTag, "failed"));
        }

        _failures.Add(1, new KeyValuePair<string, object?>(TaskTag, job));
    }

    private static IEnumerable<Measurement<T>> Observe<T>(ConcurrentDictionary<string, T> values)
        where T : struct =>
        values.Select(static entry => new Measurement<T>(
            entry.Value,
            new KeyValuePair<string, object?>(TaskTag, entry.Key)));
}
