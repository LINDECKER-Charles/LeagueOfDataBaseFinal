using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Monitor;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Domain.Versions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Endpoints;

/// <summary>
/// <c>GET /api/admin/prerender/passes/{id}/versions/{version}</c>: the units of one version of
/// a pass, a row per locale; <c>400</c> for a malformed version, <c>404</c> for no such pass.
/// </summary>
internal static class PrerenderPassVersionEndpoint
{
    /// <summary>The field a malformed version is refused under.</summary>
    public const string VersionField = "version";

    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapGet("/passes/{id:long}/versions/{version}", ReadAsync)
            .WithName("readAdminPrerenderPassVersion")
            .WithSummary("The units of one version of a pass, a row per locale.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<PrerenderVersionUnits>, AccountProblem>> ReadAsync(
        [FromRoute] long id,
        [FromRoute] string version,
        [FromServices] PassMonitor monitor,
        CancellationToken cancellationToken)
    {
        if (!PatchVersion.TryParse(version, out var read))
        {
            var errors = new FieldErrors();
            errors.Add(VersionField, FieldErrors.Malformed);
            return errors.ToProblem();
        }

        var units = await monitor.VersionAsync(id, read, cancellationToken);
        return units is null ? PrerenderProblems.NotFound() : TypedResults.Ok(units);
    }
}
