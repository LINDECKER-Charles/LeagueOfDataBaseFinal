using LoDb.Api.Modules.Admin.Http;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>
/// <c>GET /api/admin/builds</c>: a page of the builds of every account whose name or
/// champion holds the search, public, private or both, newest first.
/// </summary>
internal static class BuildListEndpoint
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapGet("/", SearchAsync)
            .WithName("searchAdminBuilds")
            .WithSummary("A page of the builds matching a search, newest first.");

    private static async Task<AdminBuildPage> SearchAsync(
        [AsParameters] BuildSearch search,
        [FromServices] BuildDirectory directory,
        CancellationToken cancellationToken) =>
        await directory.SearchAsync(search, AdminPaging.PageOf(search.Page), cancellationToken);
}
