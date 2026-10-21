using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Endpoints;

/// <summary>
/// <c>POST /api/admin/prerender/passes</c>: opens a pass over a perimeter, writing the served
/// epoch or a newer one (<c>admin.prerender_start</c>); <c>409</c> while another is active or
/// while the pre-rendering is not configured on this host.
/// </summary>
internal static class PrerenderStartEndpoint
{
    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapPost("/passes", StartAsync)
            .WithName("startAdminPrerenderPass")
            .WithSummary("Opens a pass of the pre-rendering, running at once.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Created<PrerenderPassStarted>, AccountProblem>> StartAsync(
        [FromBody] PassRequest? request,
        [FromServices] PrerenderDesk desk,
        CancellationToken cancellationToken)
    {
        var (started, problem) = await desk.StartAsync(request ?? new(), cancellationToken);
        return started is null ? problem! : TypedResults.Created((string?)null, started);
    }
}
