using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Summoners;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/summoners</c>: every summoner spell, Classic twins
/// included, in the upstream order.
/// </summary>
internal sealed record SummonerList
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public string? ContentLanguage { get; init; }

    public required int Total { get; init; }

    public int? Page { get; init; }

    public int? Size { get; init; }

    public required SummonerFacets Facets { get; init; }

    public required IReadOnlyList<SummonerCard> Entries { get; init; }

    /// <summary>The spells in the list's order, which the pages' pager follows.</summary>
    public static IReadOnlyList<SummonerSpell> EntriesOf(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Summoners.Entries;
    }

    /// <summary>The spells on either side of <paramref name="spell"/> in the list.</summary>
    public static DetailNeighbours NeighboursOf(SummonerSpell spell, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return DetailNeighbours.Around(
            EntriesOf(catalog),
            entry => string.Equals(entry.Id, spell.Id, StringComparison.Ordinal),
            entry => DetailNeighbour.Of(entry, catalog));
    }

    public static SummonerList Of(CatalogSnapshot catalog, PageRequest page, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(page);
        var spells = catalog.Summoners;
        return new SummonerList
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = spells.ContentLanguage?.Code,
            Total = EntriesOf(catalog).Count,
            Page = page.Page,
            Size = page.Size,
            Facets = SummonerFacets.Of(catalog),
            Entries = [.. page.Slice(EntriesOf(catalog))
                .Select(spell => SummonerCard.Of(spell, catalog, images))],
        };
    }
}
