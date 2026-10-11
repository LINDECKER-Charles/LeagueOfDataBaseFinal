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
    private const int DefaultImageVerdictsMinutes = 10;
    private const int DefaultImageVerdictVersions = 32;
    private const int DefaultArtVariantsMinutes = 30;
    private const int DefaultArtVariantsConcurrency = 2;

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

    /// <summary>
    /// How long the image verdicts read for a version are reused before the manifest is read
    /// again: the delay before a verdict replaced by <c>ingest --force</c> shows.
    /// </summary>
    public TimeSpan ImageVerdictsLifetime { get; set; } =
        TimeSpan.FromMinutes(DefaultImageVerdictsMinutes);

    /// <summary>
    /// Versions whose image verdicts are memoised, the least recently read one dropped first;
    /// a version holds the verdicts of the images its pages showed, a few hundred kilobytes
    /// at most.
    /// </summary>
    public int ImageVerdictVersions { get; set; } = DefaultImageVerdictVersions;

    /// <summary>
    /// Period of the job storing the WebP variants of the latest version's champion art: a
    /// promoted version shows them within this delay, the hotlinked art meanwhile.
    /// </summary>
    public TimeSpan ArtVariantsPeriod { get; set; } =
        TimeSpan.FromMinutes(DefaultArtVariantsMinutes);

    /// <summary>
    /// Arts fetched and transcoded at once by that job: each transcode takes a processor
    /// for some tens of milliseconds, taken from the pages the API serves meanwhile.
    /// </summary>
    public int ArtVariantsConcurrency { get; set; } = DefaultArtVariantsConcurrency;
}
