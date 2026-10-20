using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// <c>POST /api/admin/prerender/passes/{id}/pause</c>, <c>…/resume</c> and <c>…/cancel</c>:
/// moves a pass, which the worker sees before its next page (<c>admin.prerender_pause</c>,
/// <c>_resume</c>, <c>_cancel</c>). A cancelled pass keeps what it wrote.
/// </summary>
internal static class PrerenderPassEndpoints
{
    public static void Map(IEndpointRouteBuilder prerender)
    {
        Moving(prerender.MapPost("/passes/{id:long}/pause", PauseAsync))
            .WithName("pauseAdminPrerenderPass")
            .WithSummary("Pauses a running pass; it resumes where it stopped.");
        Moving(prerender.MapPost("/passes/{id:long}/resume", ResumeAsync))
            .WithName("resumeAdminPrerenderPass")
            .WithSummary("Resumes a paused pass at its cursor.");
        Moving(prerender.MapPost("/passes/{id:long}/cancel", CancelAsync))
            .WithName("cancelAdminPrerenderPass")
            .WithSummary("Ends an active pass for good; its pages stay in the store.");
    }

    private static RouteHandlerBuilder Moving(RouteHandlerBuilder endpoint) =>
        endpoint
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<NoContent, AccountProblem>> PauseAsync(
        [FromRoute] long id,
        [FromServices] PrerenderDesk desk,
        CancellationToken cancellationToken) =>
        Answer(await desk.PauseAsync(id, cancellationToken));

    private static async Task<Results<NoContent, AccountProblem>> ResumeAsync(
        [FromRoute] long id,
        [FromServices] PrerenderDesk desk,
        CancellationToken cancellationToken) =>
        Answer(await desk.ResumeAsync(id, cancellationToken));

    private static async Task<Results<NoContent, AccountProblem>> CancelAsync(
        [FromRoute] long id,
        [FromServices] PrerenderDesk desk,
        CancellationToken cancellationToken) =>
        Answer(await desk.CancelAsync(id, cancellationToken));

    private static Results<NoContent, AccountProblem> Answer(AccountProblem? problem) =>
        problem is null ? TypedResults.NoContent() : problem;
}
