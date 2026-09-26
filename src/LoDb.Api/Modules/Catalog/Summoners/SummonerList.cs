using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
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
            Total = spells.Entries.Count,
            Page = page.Page,
            Size = page.Size,
            Facets = SummonerFacets.Of(catalog),
            Entries = [.. page.Slice(spells.Entries)
                .Select(spell => SummonerCard.Of(spell, catalog, images))],
        };
    }
}
