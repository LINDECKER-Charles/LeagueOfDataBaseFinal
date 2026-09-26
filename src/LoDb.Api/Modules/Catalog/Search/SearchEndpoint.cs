using LoDb.Api.Modules.Catalog.Http;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Snapshots;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.Search;

/// <summary>
/// The catalog search, accent- and case-insensitive; it replaces the three legacy
/// <c>/api/*/search/{name}</c> routes the apps call.
/// </summary>
internal static class SearchEndpoint
{
    public static void Map(RouteGroupBuilder catalog) =>
        catalog.MapGet("/search", SearchAsync)
            .WithName("searchCatalog")
            .WithSummary("Entries whose name, or id, holds the query; images resolved.");

    private static async Task<Results<CachedJson<SearchResults>, CatalogProblem>> SearchAsync(
        [AsParameters] SearchRequest request)
    {
        if (!SearchQuery.TryParse(request.Q, out var query))
        {
            return CatalogProblem.InvalidQuery(
                $"q takes {SearchQuery.MinLength} to {SearchQuery.MaxLength} characters.");
        }

        if (!SearchTypes.TryParse(request.Types, out var types))
        {
            return CatalogProblem.InvalidQuery($"types takes {SearchTypes.Allowed}.");
        }

        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var catalog = context.Catalog;
        var hits = catalog.Search(query, types, SearchResults.LimitPerType);
        var images = await context.ResolveAsync(
            hits.Select(hit => SearchResultHit.ImageOf(hit, catalog)),
            ColdDemand.Synchronous,
            request.CancellationToken);
        List<SearchResultHit> shown =
            [.. hits.Select(hit => SearchResultHit.Of(hit, catalog, images))];
        return context.Answer(SearchResults.Of(catalog, query, shown), images);
    }
}
