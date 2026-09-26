using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Legacy;

/// <summary>
/// The canonical path of the entry an old <c>{name}</c> designated in a catalog.
/// </summary>
/// <remarks>
/// The old name was the Data Dragon key, matched exactly: the map key for champions, items
/// and summoner spells, the <c>key</c> of a rune path ("Domination"), whose new id is
/// numeric.
/// </remarks>
internal static class LegacyEntityLookup
{
    /// <returns>The path, or <see langword="null"/> when the catalog holds no such entry.</returns>
    public static CanonicalPath? Find(CatalogSnapshot catalog, ResourceType resource, string name)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(name);
        return resource switch
        {
            ResourceType.Champions => catalog.Champions.Find(name) is { } champion
                ? catalog.PathOf(champion)
                : null,
            ResourceType.Items => catalog.Items.Find(name) is { } item
                ? catalog.PathOf(item)
                : null,
            ResourceType.Runes => catalog.Runes.Entries
                .FirstOrDefault(tree => string.Equals(tree.Key, name, StringComparison.Ordinal))
                is { } tree
                ? catalog.PathOf(tree)
                : null,
            ResourceType.Summoners => catalog.Summoners.Find(name) is { } spell
                ? catalog.PathOf(spell)
                : null,
            _ => null,
        };
    }
}
