using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Robots.Retention;

namespace LoDb.Api.Workers.Analytics;

/// <summary>
/// The daily upkeep of the robots journal, on one instance at a time: the hits older than
/// <c>LoDb:Robots:HitRetention</c> (30 days) deleted. Their daily counts are kept.
/// </summary>
internal sealed class RobotRetentionJob(
    PeriodicJobServices services,
    IRobotRetention retention) : PeriodicJob(services)
{
    private const string Unit = "robot hits deleted";

    public override string Name => "robots.retention";

    public override TimeSpan Period => TimeSpan.FromDays(1);

    protected override async Task<JobRunSummary> RunOnceAsync(
        CancellationToken cancellationToken) =>
        new(await retention.ApplyAsync(cancellationToken), Unit);
}
