using LoDb.Domain.Catalog;
using LoDb.Domain.States;
using LoDb.Domain.States.Projections;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// The projection of an entity as the index builds it: among the entities with a page, found
/// by its folded key, the index's identity across versions.
/// </summary>
internal static class ListedProjections
{
    public static IReadOnlyList<ProjectedField> Of(CatalogSnapshot catalog, IndexedEntity entity) =>
        entity.Resource switch
        {
            ResourceType.Champions => ChampionProjection.Of(Listed(
                ListedEntities.Champions(catalog),
                entity,
                static entry => entry.Summary.Id)),
            ResourceType.Items => ItemProjection.Of(
                Listed(ListedEntities.Items(catalog), entity, static entry => entry.Id)),
            ResourceType.Runes => RuneTreeProjection.Of(Listed(
                ListedEntities.RuneTrees(catalog),
                entity,
                static entry => EntityKey.Of(entry.Id))),
            _ => SummonerProjection.Of(
                Listed(ListedEntities.Summoners(catalog), entity, static entry => entry.Id)),
        };

    private static TEntry Listed<TEntry>(
        IReadOnlyList<TEntry> entries,
        IndexedEntity entity,
        Func<TEntry, string> idOf) =>
        Assert.Single(entries, entry => EntityKey.Of(idOf(entry)) == entity.Key);
}
