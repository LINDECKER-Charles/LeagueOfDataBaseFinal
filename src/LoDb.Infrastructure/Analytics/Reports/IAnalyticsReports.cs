namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>The reports of the admin, read from <c>analytics_daily</c>.</summary>
public interface IAnalyticsReports
{
    /// <summary>
    /// The report of the period ending today (UTC). Today is folded from its views on the
    /// spot, as the legacy report did; the other days are read from their aggregates.
    /// </summary>
    Task<AnalyticsReport> BuildAsync(AnalyticsRange range, CancellationToken cancellationToken);
}
