using LoDb.Api.Modules.Catalog.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>The page of changes of a patch.</summary>
internal static class ChangesEndpoint
{
    public static void Map(RouteGroupBuilder catalog) =>
        catalog.MapGet("/changes", GetAsync)
            .WithName("getPatchChanges")
            .WithSummary("What a patch changed: entities whose state opens there, and retired.");

    // The contract only, until the index answers it: no version has a page of changes yet.
    private static Task<Results<CachedJson<PatchChanges>, CatalogProblem>> GetAsync(
        [AsParameters] ChangesRequest request) =>
        Task.FromResult<Results<CachedJson<PatchChanges>, CatalogProblem>>(
            CatalogProblem.NoPatchPage(request.Version));
}
