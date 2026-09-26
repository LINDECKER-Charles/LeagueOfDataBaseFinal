using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary>
/// The facet options the data decides, over the whole list; the fixed ones (range classes)
/// are enums of the contract.
/// </summary>
internal sealed record ChampionFacets
{
    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<string> Resources { get; init; }

    public static ChampionFacets Of(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var champions = catalog.Champions.Entries;
        return new ChampionFacets
        {
            Tags = [.. champions
                .SelectMany(static champion => champion.Summary.Tags)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            Resources = [.. champions
                .Select(catalog.ResourceTokenOf)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
        };
    }
}
