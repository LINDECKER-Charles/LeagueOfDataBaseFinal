using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Normalization;

/// <summary>
/// Reads and normalizes the datasets of a (version, language), ready for the
/// <c>IDatasetStore</c> through <see cref="Serialization.DatasetSerializer"/>.
/// </summary>
/// <remarks>
/// Each dataset falls back from the requested language to <c>en_US</c>, then to an empty
/// document (UP 1, UP 2): a returned document is always complete and safe to persist. A
/// transient failure or an unreadable file throws an
/// <see cref="Egress.Errors.EgressException"/> instead, so that nothing is persisted.
/// </remarks>
public interface IDdragonDatasets
{
    /// <summary>
    /// The chromas of a version: its CommunityDragon patch, else <c>latest</c>, else none.
    /// </summary>
    Task<ChromaCatalog> ReadChromasAsync(PatchVersion version, CancellationToken cancellationToken);

    /// <summary>
    /// Every champion with its details: <c>championFull.json</c>, else the per-champion files,
    /// else the summary alone for each champion whose file is missing (UP 3).
    /// </summary>
    Task<DatasetDocument<ChampionDetail>> ReadChampionsAsync(
        DatasetScope scope,
        ChromaCatalog chromas,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every item, debris included for the recipes, names reduced and twins linked (UP 6,
    /// UP 10).
    /// </summary>
    Task<DatasetDocument<Item>> ReadItemsAsync(
        DatasetScope scope,
        CancellationToken cancellationToken);

    /// <summary>Every rune path; empty before 7.22.1, which ships none (UP 1).</summary>
    Task<DatasetDocument<RuneTree>> ReadRunesAsync(
        DatasetScope scope,
        CancellationToken cancellationToken);

    /// <summary>Every summoner spell, twins linked (UP 6).</summary>
    Task<DatasetDocument<SummonerSpell>> ReadSummonersAsync(
        DatasetScope scope,
        CancellationToken cancellationToken);
}
