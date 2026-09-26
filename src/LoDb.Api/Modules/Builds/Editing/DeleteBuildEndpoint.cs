using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// <c>DELETE /api/builds/{id}</c>: removes a build of the signed-in account, its votes with
/// it; another account's build is a 404.
/// </summary>
internal sealed class DeleteBuildEndpoint(
    BuildAccounts accounts,
    OwnedBuilds owned,
    LoDbDbContext db,
    BuildAudit audit)
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapDelete(
                "/{id:int}",
                static (
                    [FromRoute] int id,
                    [FromServices] DeleteBuildEndpoint endpoint,
                    HttpContext context) => endpoint.DeleteAsync(id, context))
            .WithName("deleteBuild")
            .WithSummary("Removes a build of the signed-in account, and its votes.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<NoContent, BuildProblem>> DeleteAsync(int id, HttpContext context)
    {
        if (await accounts.FindAsync(context.User) is not { } owner)
        {
            return BuildProblem.SignedOut();
        }

        var aborted = context.RequestAborted;
        if (await owned.FindAsync(id, owner.Id, aborted) is not { } build)
        {
            return BuildProblem.NotFound();
        }

        // The votes go with it: their foreign key cascades.
        db.Builds.Remove(build);
        await db.SaveChangesAsync(aborted);
        await audit.DeletedAsync(build, aborted);
        return TypedResults.NoContent();
    }
}
