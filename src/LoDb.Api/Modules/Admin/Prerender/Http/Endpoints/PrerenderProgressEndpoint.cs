using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Monitor;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Endpoints;

/// <summary>
/// <c>GET /api/admin/prerender/passes/{id}/progress?since={iso}</c>: where a pass is, active or
/// over, for the live monitor; with a recent <c>since</c>, only the versions changed after it.
/// <c>404</c> for no such pass.
/// </summary>
internal static class PrerenderProgressEndpoint
{
    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapGet("/passes/{id:long}/progress", ReadAsync)
            .WithName("readAdminPrerenderPassProgress")
            .WithSummary("The progress of a pass, whole or since a time, for the live monitor.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<PrerenderPassProgress>, AccountProblem>> ReadAsync(
        [FromRoute] long id,
        [FromQuery] DateTimeOffset? since,
        [FromServices] PassMonitor monitor,
        CancellationToken cancellationToken)
    {
        var progress = await monitor.ProgressAsync(id, since, cancellationToken);
        return progress is null ? PrerenderProblems.NotFound() : TypedResults.Ok(progress);
    }
}
