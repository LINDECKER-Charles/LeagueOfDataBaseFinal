using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// The catalogs: one immutable <see cref="Snapshots.CatalogSnapshot"/> per (version,
/// language), loaded from the stored datasets and held in memory.
/// </summary>
/// <remarks>
/// <para>
/// A catalog is built once and kept in a least-recently-used cache of
/// <c>LoDb:Catalog:MaxEntries</c> entries; concurrent reads of a cold one share its load.
/// Datasets the store lacks are ingested as the <see cref="ColdDemand"/> allows: waited for,
/// queued, or not at all. A catalog in another language than en_US needs the en_US datasets
/// of its version too, where slugs and resource tokens are read (UP 13): both are ingested,
/// or queued, together.
/// </para>
/// <para>
/// Only a loaded catalog is cached: an upstream failure is thrown, and the next read tries
/// again (heritage § 4). The ready versions and the latest one are cached
/// <c>LoDb:Catalog:VersionsLifetime</c>, the list of Data Dragon ten minutes.
/// </para>
/// </remarks>
public interface ICatalogReader
{
    /// <summary>Every version Data Dragon lists, the ready ones and the latest one.</summary>
    Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The newest promoted version; <see langword="null"/> until the first ingestion
    /// completes.
    /// </summary>
    Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken);

    /// <summary>Every language Data Dragon lists.</summary>
    Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(CancellationToken cancellationToken);

    /// <summary>The catalog of a (version, language), or why there is none yet.</summary>
    /// <exception cref="Egress.Errors.EgressException">
    /// Data Dragon failed during a synchronous ingestion: nothing is stored nor cached.
    /// </exception>
    Task<CatalogLoad> GetAsync(
        PatchVersion version,
        DdragonLanguage language,
        ColdDemand demand,
        CancellationToken cancellationToken);
}
