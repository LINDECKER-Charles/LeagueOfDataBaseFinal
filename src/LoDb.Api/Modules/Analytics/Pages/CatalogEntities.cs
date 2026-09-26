using LoDb.Domain.Catalog;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Analytics.Pages;

/// <summary>
/// The entity a detail's id designates in a catalog: its canonical path, which the front
/// redirects any other spelling to, and the key the legacy stack counted it under.
/// </summary>
/// <remarks>
/// The legacy keys are the Data Dragon ids of its routes: <c>Ahri</c>, <c>1004</c>,
/// <c>SummonerFlash</c>, and the key of a rune path (<c>Domination</c>), not its number.
/// </remarks>
internal static class CatalogEntities
{
    /// <returns>Null when the catalog holds no such entity.</returns>
    public static (string Key, string CanonicalPath)? Find(
        CatalogSnapshot catalog,
        ResourceType resource,
        string id)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return resource switch
        {
            ResourceType.Champions => catalog.Champions.Find(id) is { } champion
                ? (champion.Summary.Id, catalog.PathOf(champion).Value)
                : null,
            ResourceType.Items => catalog.Items.Find(id) is { } item
                ? (item.Id, catalog.PathOf(item).Value)
                : null,
            ResourceType.Runes => catalog.Runes.Find(id) is { } tree
                ? (tree.Key, catalog.PathOf(tree).Value)
                : null,
            ResourceType.Summoners => catalog.Summoners.Find(id) is { } spell
                ? (spell.Id, catalog.PathOf(spell).Value)
                : null,
            _ => null,
        };
    }
}
