namespace LoDb.Infrastructure.Analytics;

/// <summary>
/// Capture, batching and retention of the page views (<c>LoDb:Analytics</c>), checked when
/// the host starts.
/// </summary>
/// <remarks>
/// With the defaults, a view reaches <c>analytics_event</c> within about two seconds and is
/// dropped after 13 months, the span the CNIL allows for audience measurement. Its address
/// and user agent only serve its capture (visitor hash, browser, country) and are no longer
/// stored; the views written before still have theirs erased after 30 days. The daily
/// aggregates hold neither and stay, bounded in visitors and referring hosts.
/// </remarks>
public sealed class AnalyticsOptions
{
    public const string SectionName = "LoDb:Analytics";

    /// <summary>
    /// The legacy stack's setting of the GeoLite2 database, read when
    /// <see cref="GeoIpDatabase"/> is empty.
    /// </summary>
    public const string LegacyGeoIpKey = "GEOIP_DB_PATH";

    private const int DefaultQueueCapacity = 10_000;
    private const int DefaultBatchSize = 500;
    private const int DefaultBatchWindowSeconds = 2;
    private const int DefaultClientDataDays = 30;
    private const int DefaultEventRetentionMonths = 13;
    private const int DefaultPartitionsAhead = 7;
    private const int DefaultClientDataEraseDays = 3;
    private const int DefaultMaxVisitorsPerDay = 200_000;
    private const int DefaultMaxRefererHostsPerDay = 200;

    /// <summary>
    /// Key of the visitor hash. The legacy stack's <c>APP_SECRET</c> keeps the visitors of
    /// both stacks the same; without one, each process draws its own and a visitor seen
    /// before and after a restart counts twice.
    /// </summary>
    public string? VisitorKey { get; set; }

    /// <summary>
    /// Path of a GeoLite2 Country database; without one, no view gets a country.
    /// </summary>
    public string? GeoIpDatabase { get; set; }

    /// <summary>Views held in memory for the writer; beyond, the next ones are dropped.</summary>
    public int QueueCapacity { get; set; } = DefaultQueueCapacity;

    /// <summary>Views written by one binary copy at most.</summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>Wait after a first view, so that the next ones join its copy.</summary>
    public TimeSpan BatchWindow { get; set; } = TimeSpan.FromSeconds(DefaultBatchWindowSeconds);

    /// <summary>Age at which the address and the user agent of a view are erased.</summary>
    public TimeSpan ClientDataRetention { get; set; } = TimeSpan.FromDays(DefaultClientDataDays);

    /// <summary>
    /// Days before the day of the retention's cutoff that a run still erases, so that two
    /// runs overlap and none scans the older partitions again. The daily job runs at most
    /// once a day, and a host restarted before it is due waits a whole day from its start:
    /// two runs can be up to two days apart, three after a failed run. After a longer
    /// outage, raise it for one run.
    /// </summary>
    public int ClientDataEraseDays { get; set; } = DefaultClientDataEraseDays;

    /// <summary>Distinct visitors a day keeps; beyond, it is flagged as truncated.</summary>
    public int MaxVisitorsPerDay { get; set; } = DefaultMaxVisitorsPerDay;

    /// <summary>
    /// Referring hosts a day counts by name; the least counted beyond fall under
    /// <c>(autres)</c>.
    /// </summary>
    public int MaxRefererHostsPerDay { get; set; } = DefaultMaxRefererHostsPerDay;

    /// <summary>Months a view is kept: its day's partition is dropped afterwards.</summary>
    public int EventRetentionMonths { get; set; } = DefaultEventRetentionMonths;

    /// <summary>Days after today whose partition exists ahead of their first view.</summary>
    public int PartitionsAhead { get; set; } = DefaultPartitionsAhead;
}
