using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Items;

/// <summary>
/// The facet options the data decides, over the whole list; the fixed ones (editions, tiers,
/// maps, stats) are enums of the contract or of <c>/api/meta</c>.
/// </summary>
internal sealed record ItemFacets
{
    /// <summary>Every tag the listed items carry, as Data Dragon names them.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    public static ItemFacets Of(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new ItemFacets
        {
            Tags = [.. catalog.ListedItems
                .SelectMany(static item => item.Tags)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
        };
    }
}
