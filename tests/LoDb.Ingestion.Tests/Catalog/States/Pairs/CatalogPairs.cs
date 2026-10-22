using LoDb.Domain.States;
using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Projections;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Tests.Catalog.States.Pairs;

/// <summary>
/// Entities of two recorded en_US catalogs, read the way the index reads them: by folded key
/// among the entities with a page, through their displayed projection.
/// </summary>
internal static class CatalogPairs
{
    public static Task<CatalogSnapshot> EnglishAsync(string version) =>
        CatalogFixtures.ReadAsync(PatchVersion.Parse(version), CatalogFixtures.English);

    public static async Task<IReadOnlyList<ProjectedField>> ItemAsync(string version, string id)
    {
        var catalog = await EnglishAsync(version);
        return ItemProjection.Of(
            Listed(ListedEntities.Items(catalog), id, static item => item.Id));
    }

    public static async Task<IReadOnlyList<ProjectedField>> ChampionAsync(
        string version,
        string id)
    {
        var catalog = await EnglishAsync(version);
        return ChampionProjection.Of(Listed(
            ListedEntities.Champions(catalog),
            id,
            static champion => champion.Summary.Id));
    }

    /// <summary>
    /// The dotted keys the index stores with the state <paramref name="after"/> opens.
    /// </summary>
    public static IReadOnlyList<string> ChangedKeys(
        IReadOnlyList<ProjectedField> before,
        IReadOnlyList<ProjectedField> after) =>
        [.. EntityDiff.ChangedKeys(before, after).Select(static key => key.ToDotted())];

    // The entity whose folded key is the folded id: the index's identity across versions.
    public static TEntry Listed<TEntry>(
        IReadOnlyList<TEntry> entries,
        string id,
        Func<TEntry, string> idOf) =>
        Assert.Single(entries, entry => EntityKey.Of(idOf(entry)) == EntityKey.Of(id));
}
