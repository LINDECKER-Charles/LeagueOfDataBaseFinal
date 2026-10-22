using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Building;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline.Datasets;

namespace LoDb.Ingestion.Catalog.States.Storage;

/// <summary>
/// The entities of a version, from its stored en_US datasets: read directly, one catalog at a
/// time, never through the catalogs the API holds in memory nor their cold gate.
/// </summary>
internal sealed class EnglishEntities(StoredDatasets stored)
{
    /// <returns>The entities; null while one of the four datasets is not stored.</returns>
    public async Task<VersionEntities?> ReadAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var datasets = await stored.LoadAsync(ScopeOf(version), cancellationToken)
            .ConfigureAwait(false);
        return datasets is null
            ? null
            : VersionEntities.Of(
                new CatalogSnapshot(version, DdragonLanguage.EnUs, datasets, english: null));
    }

    /// <summary>Where the en_US datasets of a version are stored.</summary>
    public static DatasetScope ScopeOf(PatchVersion version) =>
        new() { Version = version, Language = DdragonLanguage.EnUs };
}
