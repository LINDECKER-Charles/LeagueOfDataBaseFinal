using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// <c>GET /api/builds/{id}</c>: a build of the signed-in account, as its editor loads it;
/// another account's build is a 404.
/// </summary>
internal sealed class ReadBuildEndpoint(BuildAccounts accounts, OwnedBuilds owned)
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapGet(
                "/{id:int}",
                static (
                    [FromRoute] int id,
                    [FromServices] ReadBuildEndpoint endpoint,
                    HttpContext context) => endpoint.ReadAsync(id, context))
            .WithName("getBuild")
            .WithSummary("A build of the signed-in account, as stored, ghosts included.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<EditableBuild>, BuildProblem>> ReadAsync(
        int id,
        HttpContext context)
    {
        if (await accounts.FindAsync(context.User) is not { } owner)
        {
            return BuildProblem.SignedOut();
        }

        return await owned.FindAsync(id, owner.Id, context.RequestAborted) is { } build
            ? TypedResults.Ok(EditableBuild.Of(build))
            : BuildProblem.NotFound();
    }
}
