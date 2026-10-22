using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Http;

/// <summary>
/// <c>GET /api/admin/prerender/revision</c>: the queue of the pages of the closed states, which
/// each promotion fills and its drain renders again into the served epoch (and the one the
/// active pass writes): whether it runs, what it holds by epoch, what it wrote lately.
/// </summary>
internal static class PrerenderRevisionEndpoint
{
    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapGet("/revision", ReadAsync)
            .WithName("readAdminPrerenderRevision")
            .WithSummary("The queue of the pages of the states a promotion closed.");

    private static async Task<PrerenderRevisionReport> ReadAsync(
        [FromServices] RevisionReader reader,
        CancellationToken cancellationToken) =>
        await reader.ReadAsync(cancellationToken);
}
