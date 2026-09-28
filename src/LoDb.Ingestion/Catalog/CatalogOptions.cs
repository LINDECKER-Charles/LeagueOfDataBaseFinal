namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Settings of the catalog (<c>LoDb:Catalog</c>), checked when the host starts.
/// </summary>
public sealed class CatalogOptions
{
    public const string SectionName = "LoDb:Catalog";

    private const int DefaultMaxEntries = 16;
    private const int DefaultVersionsSeconds = 60;

    /// <summary>
    /// Catalogs kept in memory, one per (version, language), a few megabytes each; the least
    /// recently read one goes first.
    /// </summary>
    public int MaxEntries { get; set; } = DefaultMaxEntries;

    /// <summary>
    /// How long the latest and ready versions are cached: a promotion shows within this delay
    /// on every instance.
    /// </summary>
    public TimeSpan VersionsLifetime { get; set; } = TimeSpan.FromSeconds(DefaultVersionsSeconds);
}
