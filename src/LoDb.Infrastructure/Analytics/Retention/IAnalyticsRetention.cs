namespace LoDb.Infrastructure.Analytics.Retention;

/// <summary>
/// The daily upkeep of <c>analytics_event</c>: its partitions ahead of time, and the
/// retention the CNIL allows behind.
/// </summary>
public interface IAnalyticsRetention
{
    /// <summary>
    /// Creates the partitions from yesterday to <see cref="AnalyticsOptions.PartitionsAhead"/>
    /// days ahead, drops those past <see cref="AnalyticsOptions.EventRetentionMonths"/>, and
    /// erases the address and the user agent of the views older than
    /// <see cref="AnalyticsOptions.ClientDataRetention"/>.
    /// </summary>
    Task<RetentionSummary> ApplyAsync(CancellationToken cancellationToken);
}
