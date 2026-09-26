using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary><c>GET /api/catalog/{version}/{lang}/champions</c>.</summary>
internal sealed record ChampionList
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    /// <summary>The language the entries are written in, en_US after a fallback.</summary>
    public string? ContentLanguage { get; init; }

    /// <summary>Entries of the whole list, whatever the page.</summary>
    public required int Total { get; init; }

    public int? Page { get; init; }

    public int? Size { get; init; }

    public required ChampionFacets Facets { get; init; }

    public required IReadOnlyList<ChampionCard> Entries { get; init; }

    public static ChampionList Of(CatalogSnapshot catalog, PageRequest page, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(page);
        var champions = catalog.Champions;
        return new ChampionList
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = champions.ContentLanguage?.Code,
            Total = champions.Entries.Count,
            Page = page.Page,
            Size = page.Size,
            Facets = ChampionFacets.Of(catalog),
            Entries = [.. page.Slice(champions.Entries)
                .Select(champion => ChampionCard.Of(champion, catalog, images))],
        };
    }
}
