namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Settings of the catalog (<c>LoDb:Catalog</c>), checked when the host starts.
/// </summary>
/// <remarks>
/// The catalogs of the latest version in the pinned languages are held apart and never
/// evicted; every other (version, language) shares the <see cref="MaxEntries"/> places of a
/// least-recently-read cache. A catalog no memory holds is loaded from the stored datasets
/// only once admitted: a few at a time, at a bounded rate, the renderer's calls apart from
/// the others.
/// </remarks>
public sealed class CatalogOptions
{
    public const string SectionName = "LoDb:Catalog";

    private const int DefaultMaxEntries = 16;
    private const int DefaultVersionsSeconds = 60;
    private const int DefaultPinnedBudgetMegabytes = 256;
    private const int DefaultColdLoadConcurrency = 2;
    private const int DefaultColdLoadsPerSecond = 3;
    private const int DefaultPublicColdLoadsPerSecond = 1;
    private const int DefaultColdLoadBurst = 6;
    private const int DefaultInternalColdLoadWaitSeconds = 2;
    private const int DefaultMaxPendingLoads = 16;

    /// <summary>
    /// Catalogs kept in memory besides the pinned ones, one per (version, language), about
    /// 7 MiB each; the least recently read one goes first.
    /// </summary>
    public int MaxEntries { get; set; } = DefaultMaxEntries;

    /// <summary>
    /// How long the latest and ready versions are cached: a promotion shows within this delay
    /// on every instance, the pinned catalogs included.
    /// </summary>
    public TimeSpan VersionsLifetime { get; set; } = TimeSpan.FromSeconds(DefaultVersionsSeconds);

    /// <summary>
    /// Data Dragon languages whose latest catalog is pinned, comma-separated, most read first;
    /// unset, the language of every interface locale, en_US first.
    /// </summary>
    public string? PinnedLanguages { get; set; }

    /// <summary>
    /// Memory the pinned catalogs may take, in mebibytes, as estimated from their stored
    /// datasets: the languages that would exceed it stay in the evictable cache. The 21
    /// interface languages take about 160 MiB (16.20.1); with the evictable cache, catalogs
    /// stay under 400 MiB of the API's memory.
    /// </summary>
    public int PinnedBudgetMegabytes { get; set; } = DefaultPinnedBudgetMegabytes;

    /// <summary>Catalogs loaded at once from the stored datasets, every caller included.</summary>
    public int ColdLoadConcurrency { get; set; } = DefaultColdLoadConcurrency;

    /// <summary>Catalog loads admitted per second, every caller included.</summary>
    public int ColdLoadsPerSecond { get; set; } = DefaultColdLoadsPerSecond;

    /// <summary>
    /// The share of <see cref="ColdLoadsPerSecond"/> left to public callers, who never wait;
    /// the rest is the renderer's.
    /// </summary>
    public int PublicColdLoadsPerSecond { get; set; } = DefaultPublicColdLoadsPerSecond;

    /// <summary>Loads admitted in a burst, split between the callers as the rate is.</summary>
    public int ColdLoadBurst { get; set; } = DefaultColdLoadBurst;

    /// <summary>
    /// How long an internal load may wait for its turn; it must stay well under the
    /// renderer's own timeout on the API.
    /// </summary>
    public TimeSpan InternalColdLoadWait { get; set; } =
        TimeSpan.FromSeconds(DefaultInternalColdLoadWaitSeconds);

    /// <summary>Internal loads waiting for their turn; more are refused at once.</summary>
    public int MaxPendingLoads { get; set; } = DefaultMaxPendingLoads;
}
