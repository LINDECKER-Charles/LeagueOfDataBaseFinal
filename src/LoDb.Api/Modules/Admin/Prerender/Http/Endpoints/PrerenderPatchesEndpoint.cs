using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Coverage;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Endpoints;

/// <summary>
/// <c>GET /api/admin/prerender/patches?epoch={n}</c>: every older patch and what an epoch, the
/// served one by default, holds of it locale by locale, with the bases of the launch's
/// estimates; <c>400</c> for an epoch below 1.
/// </summary>
internal static class PrerenderPatchesEndpoint
{
    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapGet("/patches", ReadAsync)
            .WithName("readAdminPrerenderPatches")
            .WithSummary("The older patches and what one epoch of the store holds of each.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<PrerenderPatchesReport>, AccountProblem>> ReadAsync(
        [FromQuery] int? epoch,
        [FromServices] PatchCoverageReader reader,
        CancellationToken cancellationToken)
    {
        var (report, problem) = await reader.ReadAsync(epoch, cancellationToken);
        return report is null ? problem! : TypedResults.Ok(report);
    }
}
