using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Paths;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>
/// The entities that have a page in a catalog: every champion and rune path, the items and
/// summoner spells the lists show (debris and placeholders have none, UP 10), each id once.
/// </summary>
/// <remarks>
/// The one source of what exists in a version, for the index of states, the sitemaps and the
/// pre-render pass. The first entry of an id is the entity, as <c>CatalogResource.Find</c>
/// reads it: an id whose first entry is debris has no page, whatever follows it.
/// </remarks>
public static class ListedEntities
{
    public static IReadOnlyList<ChampionDetail> Champions(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return
        [
            .. catalog.Champions.Entries
                .DistinctBy(static champion => champion.Summary.Id, StringComparer.Ordinal),
        ];
    }

    public static IReadOnlyList<Item> Items(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return
        [
            .. catalog.Items.Entries
                .DistinctBy(static item => item.Id, StringComparer.Ordinal)
                .Where(catalog.IsListed),
        ];
    }

    public static IReadOnlyList<RuneTree> RuneTrees(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. catalog.Runes.Entries.DistinctBy(static tree => tree.Id)];
    }

    public static IReadOnlyList<SummonerSpell> Summoners(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return
        [
            .. catalog.Summoners.Entries
                .DistinctBy(static spell => spell.Id, StringComparer.Ordinal)
                .Where(catalog.IsListed),
        ];
    }

    /// <summary>
    /// Their canonical paths: the champions, the items, the rune paths, then the summoner
    /// spells, each in the upstream order.
    /// </summary>
    public static IEnumerable<CanonicalPath> PathsOf(CatalogSnapshot catalog) =>
        Champions(catalog).Select(catalog.PathOf)
            .Concat(Items(catalog).Select(catalog.PathOf))
            .Concat(RuneTrees(catalog).Select(catalog.PathOf))
            .Concat(Summoners(catalog).Select(catalog.PathOf));
}
