using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Items;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Items;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/items</c>: the items of the encyclopedia, Classic
/// ones included and debris left out, in the upstream order.
/// </summary>
internal sealed record ItemList
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public string? ContentLanguage { get; init; }

    public required int Total { get; init; }

    public int? Page { get; init; }

    public int? Size { get; init; }

    public required ItemFacets Facets { get; init; }

    public required IReadOnlyList<ItemCard> Entries { get; init; }

    /// <summary>
    /// The items the entries build into, each once, in the order they are met: the names and
    /// icons of the cards' <c>upgrades</c>.
    /// </summary>
    public required IReadOnlyList<EntityLink> Related { get; init; }

    /// <summary>The items in the list's order, which the pages' pager follows.</summary>
    public static IReadOnlyList<Item> EntriesOf(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.ListedItems;
    }

    /// <summary>The items on either side of <paramref name="item"/> in the list.</summary>
    public static DetailNeighbours NeighboursOf(Item item, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(item);
        return DetailNeighbours.Around(
            EntriesOf(catalog),
            entry => string.Equals(entry.Id, item.Id, StringComparison.Ordinal),
            entry => DetailNeighbour.Of(entry, catalog));
    }

    /// <summary>What <paramref name="shown"/> builds into, each item once.</summary>
    public static IReadOnlyList<Item> RelatedOf(IEnumerable<Item> shown, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. shown
            .SelectMany(item => ItemUpgrades.Of(item, catalog))
            .DistinctBy(static upgrade => upgrade.Id, StringComparer.Ordinal)];
    }

    public static ItemList Of(CatalogSnapshot catalog, PageRequest page, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(page);
        var items = EntriesOf(catalog);
        var shown = page.Slice(items);
        return new ItemList
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Items.ContentLanguage?.Code,
            Total = items.Count,
            Page = page.Page,
            Size = page.Size,
            Facets = ItemFacets.Of(catalog),
            Entries = [.. shown.Select(item => ItemCard.Of(item, catalog, images))],
            Related = [.. RelatedOf(shown, catalog)
                .Select(upgrade => EntityLink.Of(upgrade, catalog, images))],
        };
    }
}
