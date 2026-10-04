using LoDb.Domain.Catalog.Modes;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Summoners;

/// <summary>The facet options the data decides, over the whole list.</summary>
internal sealed record SummonerFacets
{
    /// <summary>The filterable modes some spell allows, in the facet's display order.</summary>
    public required IReadOnlyList<string> Modes { get; init; }

    /// <summary>The unlock levels, ascending.</summary>
    public required IReadOnlyList<int> Levels { get; init; }

    public static SummonerFacets Of(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var spells = catalog.Summoners.Entries;
        var allowed = spells
            .SelectMany(static spell => spell.Modes)
            .ToHashSet(StringComparer.Ordinal);
        return new SummonerFacets
        {
            Modes = [.. GameModeLabels.Facetable.Where(allowed.Contains)],
            Levels = [.. spells
                .Select(static spell => spell.SummonerLevel)
                .OfType<int>()
                .Distinct()
                .Order()],
        };
    }
}
