using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Speed;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Endpoints;

/// <summary>
/// <c>GET /api/admin/prerender/speed</c>: the operator's pace setting, what the environment
/// allows, the pace in force and the host's resources; <c>PUT</c> sets the pace, adaptive or
/// manual, live: <c>400</c> for a target out of bounds, <c>409</c> under the kill switch.
/// </summary>
internal static class PrerenderSpeedEndpoints
{
    public static void Map(IEndpointRouteBuilder prerender)
    {
        prerender.MapGet("/speed", ReadAsync)
            .WithName("readAdminPrerenderSpeed")
            .WithSummary("The pace setting of the pre-rendering, its bounds and the resources.");
        prerender.MapPut("/speed", SetAsync)
            .WithName("setAdminPrerenderSpeed")
            .WithSummary("Sets the pace of the pre-rendering, adaptive or manual, live.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Ok<PrerenderSpeedReport>> ReadAsync(
        [FromServices] SpeedDesk desk,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await desk.ReportAsync(cancellationToken));

    private static async Task<Results<NoContent, AccountProblem>> SetAsync(
        [FromBody] PrerenderSpeedRequest? request,
        [FromServices] SpeedDesk desk,
        CancellationToken cancellationToken)
    {
        var problem = await desk.SetAsync(request ?? new(), cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
