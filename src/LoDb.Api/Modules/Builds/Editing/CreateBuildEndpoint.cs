using LoDb.Api.Hosting;
using LoDb.Api.Modules.Builds.Editing.Bodies;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// <c>POST /api/builds</c>: saves a new build of the signed-in account, whose e-mail must be
/// verified.
/// </summary>
internal sealed class CreateBuildEndpoint(
    BuildAccounts accounts,
    BuildSubmissions submissions,
    LoDbDbContext db,
    BuildAudit audit,
    TimeProvider time)
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapPost(
                string.Empty,
                static (
                    [FromBody] BuildRequest request,
                    [FromQuery(Name = "lang")] string? lang,
                    [FromServices] CreateBuildEndpoint endpoint,
                    HttpContext context) => endpoint.CreateAsync(request, lang, context))
            .RequireAuthorization(AuthorizationPolicies.VerifiedEmail)
            .WithName("createBuild")
            .WithSummary("Saves a new build, pinned to the patch and the mode it was checked on.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<Created<EditableBuild>, BuildProblem, SubmissionRefusal>> CreateAsync(
        BuildRequest request,
        string? lang,
        HttpContext context)
    {
        if (await accounts.FindAsync(context.User) is not { } owner)
        {
            return BuildProblem.SignedOut();
        }

        var aborted = context.RequestAborted;
        var check = await submissions.CheckAsync(request, lang, aborted);
        if (!check.IsAccepted)
        {
            return check.Refusal;
        }

        var build = check.Submission.Create(owner.Id, StoredTime.Now(time));
        db.Builds.Add(build);
        await db.SaveChangesAsync(aborted);
        await audit.CreatedAsync(build, aborted);
        return TypedResults.Created(BuildRoutes.Of(build.Id), EditableBuild.Of(build));
    }
}
