using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>
/// <c>GET /api/admin/prerender/demand</c>: the automatic queue of the pages the visitors' SSR
/// refused, whether it runs, what it holds and wrote lately, and its most requested pages.
/// </summary>
internal static class PrerenderDemandEndpoint
{
    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapGet("/demand", ReadAsync)
            .WithName("readAdminPrerenderDemand")
            .WithSummary("The automatic queue of the pages the SSR refused to visitors.");

    private static async Task<PrerenderDemandReport> ReadAsync(
        [FromServices] DemandReader reader,
        CancellationToken cancellationToken) =>
        await reader.ReadAsync(cancellationToken);
}
