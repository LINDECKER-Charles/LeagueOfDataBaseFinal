using LoDb.Domain.Catalog.Items;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Items;

/// <summary>
/// What an item builds into, as its card and its page list it: the listed items of its
/// <c>into</c>, each once, in the upstream order. Debris has no page to link to (UP 10).
/// </summary>
internal static class ItemUpgrades
{
    public static IReadOnlyList<Item> Of(Item item, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. item.Into
            .Distinct(StringComparer.Ordinal)
            .Select(catalog.Items.Find)
            .OfType<Item>()
            .Where(catalog.IsListed)];
    }
}
