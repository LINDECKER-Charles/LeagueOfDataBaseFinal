using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Analytics;

/// <summary>
/// Folds the page views into <c>analytics_daily</c> every five minutes, on one instance at a
/// time: today, still open, and the closed days whose aggregate predates their close, which
/// is yesterday once just after midnight, or the days an outage left behind.
/// </summary>
/// <remarks>
/// A closed day is folded once for all after its close: the next runs only read the states
/// of the aggregates. Not audited: the admin's own rollup is (<c>admin.analytics_rollup</c>).
/// </remarks>
internal sealed class AnalyticsRollupJob(PeriodicJobServices services, IAnalyticsRollup rollup)
    : PeriodicJob(services)
{
    private const string Unit = "days";

    private static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    public override string Name => "analytics.rollup";

    public override TimeSpan Period => Every;

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        var closed = await rollup.RollupClosedAsync(cancellationToken);
        var today = await rollup.RollupTodayAsync(cancellationToken);
        return new(closed.Count + today.Count, Unit);
    }
}
