using LoDb.Api.Hosting;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// <c>PUT /api/builds/{id}</c>: replaces a build of the signed-in account, whose e-mail must
/// be verified; another account's build is a 404.
/// </summary>
internal sealed class UpdateBuildEndpoint(
    BuildAccounts accounts,
    OwnedBuilds owned,
    BuildSubmissions submissions,
    LoDbDbContext db,
    BuildAudit audit,
    TimeProvider time)
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapPut(
                "/{id:int}",
                static (
                    [FromRoute] int id,
                    [AsParameters] UpdateBuildRequest request,
                    [FromServices] UpdateBuildEndpoint endpoint) =>
                    endpoint.UpdateAsync(id, request))
            .RequireAuthorization(AuthorizationPolicies.VerifiedEmail)
            .WithName("updateBuild")
            .WithSummary("Replaces a build, pinned again to the patch and the mode checked.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<Ok<EditableBuild>, BuildProblem, SubmissionRefusal>> UpdateAsync(
        int id,
        UpdateBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = request.Context;
        if (await accounts.FindAsync(context.User) is not { } owner)
        {
            return BuildProblem.SignedOut();
        }

        var aborted = context.RequestAborted;
        if (await owned.FindAsync(id, owner.Id, aborted) is not { } build)
        {
            return BuildProblem.NotFound();
        }

        var check = await submissions.CheckAsync(request.Body, request.Lang, aborted);
        if (!check.IsAccepted)
        {
            return check.Refusal;
        }

        check.Submission.ApplyTo(build, StoredTime.Now(time));
        await db.SaveChangesAsync(aborted);
        await audit.UpdatedAsync(build, aborted);
        return TypedResults.Ok(EditableBuild.Of(build));
    }
}
