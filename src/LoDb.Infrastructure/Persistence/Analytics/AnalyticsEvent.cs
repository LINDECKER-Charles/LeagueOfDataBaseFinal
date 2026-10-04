namespace LoDb.Infrastructure.Persistence.Analytics;

/// <summary>
/// A row of <c>analytics_event</c>: one captured page view, with the fields of the legacy
/// NDJSON line (<c>RequestEvent</c>) and the origin of the capture.
/// </summary>
/// <remarks>
/// The table is partitioned by UTC day on <see cref="OccurredAt"/> and holds no partition
/// until <see cref="Partitions.IAnalyticsPartitions"/> creates them: a view outside every
/// partition is refused. Retention drops whole partitions; <see cref="Ip"/> and
/// <see cref="UserAgent"/> are erased earlier, hence nullable. The classification columns
/// keep the legacy vocabulary as text, with no check, so that the analytics zone can refine
/// it without a migration.
/// </remarks>
public sealed class AnalyticsEvent
{
    // Column sizes: the writer cuts longer values rather than lose the batch.
    public const int RouteMaxLength = 128;
    public const int PathMaxLength = 1024;
    public const int TypeMaxLength = 32;
    public const int KindMaxLength = 16;
    public const int EntityMaxLength = 128;
    public const int VersionMaxLength = 32;
    public const int LangMaxLength = 16;
    public const int LocaleMaxLength = 16;
    public const int IpMaxLength = 45;
    public const int VisitorMaxLength = 64;
    public const int UserAgentMaxLength = 512;
    public const int BrowserMaxLength = 32;
    public const int OsMaxLength = 32;
    public const int DeviceMaxLength = 16;
    public const int RefererHostMaxLength = 255;
    public const int RefererSourceMaxLength = 16;
    public const int CountryMaxLength = 2;
    public const int CountryNameMaxLength = 64;

    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public AnalyticsCaptureOrigin Origin { get; set; }

    public required string Route { get; set; }

    public required string Path { get; set; }

    /// <summary>
    /// <c>champion</c>, <c>item</c>, <c>runesReforged</c>, <c>summoner</c> or <c>home</c>.
    /// </summary>
    public required string Type { get; set; }

    /// <summary><c>home</c>, <c>list</c> or <c>detail</c>.</summary>
    public required string Kind { get; set; }

    /// <summary>Key of the entity of a detail page, such as <c>Aatrox</c>.</summary>
    public string? Entity { get; set; }

    /// <summary>HTTP status of the page.</summary>
    public short Status { get; set; }

    public string? Version { get; set; }

    /// <summary>Data Dragon language of the page, such as <c>fr_FR</c>.</summary>
    public string? Lang { get; set; }

    /// <summary>Interface locale, such as <c>fr</c>.</summary>
    public required string Locale { get; set; }

    public string? Ip { get; set; }

    /// <summary>Keyed hash of the real address and the user agent.</summary>
    public required string Visitor { get; set; }

    public string? UserAgent { get; set; }

    public required string Browser { get; set; }

    public required string Os { get; set; }

    /// <summary><c>desktop</c>, <c>mobile</c>, <c>tablet</c>, <c>bot</c> or <c>other</c>.</summary>
    public required string Device { get; set; }

    public bool IsBot { get; set; }

    public string? RefererHost { get; set; }

    /// <summary>
    /// <c>direct</c>, <c>internal</c>, <c>search</c>, <c>social</c> or <c>external</c>.
    /// </summary>
    public required string RefererSource { get; set; }

    /// <summary>ISO 3166 alpha-2 code.</summary>
    public string? Country { get; set; }

    public string? CountryName { get; set; }
}
