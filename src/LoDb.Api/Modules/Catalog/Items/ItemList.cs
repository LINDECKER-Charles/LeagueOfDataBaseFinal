using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
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

    public static ItemList Of(CatalogSnapshot catalog, PageRequest page, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(page);
        var items = catalog.ListedItems;
        return new ItemList
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Items.ContentLanguage?.Code,
            Total = items.Count,
            Page = page.Page,
            Size = page.Size,
            Facets = ItemFacets.Of(catalog),
            Entries = [.. page.Slice(items).Select(item => ItemCard.Of(item, catalog, images))],
        };
    }
}
