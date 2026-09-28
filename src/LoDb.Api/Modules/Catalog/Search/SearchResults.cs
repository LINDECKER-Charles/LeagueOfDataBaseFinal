using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Search;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/search</c>: the hits, resource by resource in the
/// order champions, items, runes, summoners, each in the upstream order and capped.
/// </summary>
internal sealed record SearchResults
{
    /// <summary>Most hits returned per resource, as the legacy search capped them.</summary>
    public const int LimitPerType = 20;

    public required string Version { get; init; }

    public required string Language { get; init; }

    /// <summary>The query as typed, trimmed.</summary>
    public required string Query { get; init; }

    public required IReadOnlyList<SearchResultHit> Hits { get; init; }

    public static SearchResults Of(
        CatalogSnapshot catalog,
        SearchQuery query,
        IReadOnlyList<SearchResultHit> hits)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(query);
        return new SearchResults
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            Query = query.Text,
            Hits = hits,
        };
    }
}
