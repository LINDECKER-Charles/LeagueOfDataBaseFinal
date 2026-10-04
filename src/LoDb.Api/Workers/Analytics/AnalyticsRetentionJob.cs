using LoDb.Infrastructure.Analytics.Retention;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Analytics;

/// <summary>
/// The daily upkeep of <c>analytics_event</c>, on one instance at a time: the partitions of
/// the coming days, then the retention the CNIL allows (addresses and user agents erased
/// after 30 days, views dropped with their partition after 13 months).
/// </summary>
/// <remarks>
/// The daily aggregates are kept: they hold neither addresses nor user agents. The summary
/// line counts the views anonymized; the partitions are logged apart.
/// </remarks>
internal sealed partial class AnalyticsRetentionJob(
    PeriodicJobServices services,
    IAnalyticsRetention retention,
    ILogger<AnalyticsRetentionJob> logger) : PeriodicJob(services)
{
    private const string Unit = "views anonymized";

    public override string Name => "analytics.retention";

    public override TimeSpan Period => TimeSpan.FromDays(1);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        var summary = await retention.ApplyAsync(cancellationToken);
        LogPartitions(logger, summary.PartitionsCreated, summary.PartitionsDropped);
        return new(summary.ClientDataErased, Unit);
    }

    [LoggerMessage(
        EventName = "analytics.retention.partitions",
        Level = LogLevel.Information,
        Message = "Analytics partitions: {Created} created, {Dropped} dropped past retention.")]
    private static partial void LogPartitions(ILogger logger, int created, int dropped);
}
