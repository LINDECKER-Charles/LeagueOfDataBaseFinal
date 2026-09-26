using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>
/// <c>DELETE /api/admin/builds/{id}</c>: removes a build of any account and its votes
/// (<c>admin.build_delete</c>).
/// </summary>
internal static class BuildDeleteEndpoint
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapDelete("/{id:int}", DeleteAsync)
            .WithName("deleteAdminBuild")
            .WithSummary("Removes a build and its votes.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, AccountProblem>> DeleteAsync(
        [FromRoute] int id,
        [FromServices] BuildModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.DeleteAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
