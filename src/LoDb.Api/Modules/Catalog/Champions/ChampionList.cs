using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Champions;
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

    /// <summary>The champions in the list's order, which the pages' pager follows.</summary>
    public static IReadOnlyList<ChampionDetail> EntriesOf(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Champions.Entries;
    }

    /// <summary>The champions on either side of <paramref name="champion"/> in the list.</summary>
    public static DetailNeighbours NeighboursOf(ChampionDetail champion, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(champion);
        var id = champion.Summary.Id;
        return DetailNeighbours.Around(
            EntriesOf(catalog),
            entry => string.Equals(entry.Summary.Id, id, StringComparison.Ordinal),
            entry => DetailNeighbour.Of(entry, catalog));
    }

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
            Total = EntriesOf(catalog).Count,
            Page = page.Page,
            Size = page.Size,
            Facets = ChampionFacets.Of(catalog),
            Entries = [.. page.Slice(EntriesOf(catalog))
                .Select(champion => ChampionCard.Of(champion, catalog, images))],
        };
    }
}
