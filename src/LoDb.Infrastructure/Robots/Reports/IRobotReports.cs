using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Robots.Reports.Detail;
using LoDb.Infrastructure.Robots.Reports.Journal;
using LoDb.Infrastructure.Robots.Reports.Views;

namespace LoDb.Infrastructure.Robots.Reports;

/// <summary>The reports of the robots journal, for the admin.</summary>
public interface IRobotReports
{
    /// <summary>Every robot of the period ending today (UTC).</summary>
    Task<RobotReport> BuildAsync(AnalyticsRange range, CancellationToken cancellationToken);

    /// <returns>Null when the key names no robot seen over the period.</returns>
    Task<RobotDetail?> DetailAsync(
        string key,
        AnalyticsRange range,
        CancellationToken cancellationToken);

    /// <summary>A page of the hits still kept, newest first.</summary>
    Task<RobotHitPage> HitsAsync(RobotHitQuery query, CancellationToken cancellationToken);
}
