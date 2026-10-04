using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>
/// <c>POST /api/admin/builds/{id}/unpublish</c>: takes a build off the public pages
/// (<c>admin.build_hide</c>).
/// </summary>
internal static class BuildUnpublishEndpoint
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapPost("/{id:int}/unpublish", UnpublishAsync)
            .WithName("unpublishAdminBuild")
            .WithSummary("Makes a build private; its author and share link still see it.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, AccountProblem>> UnpublishAsync(
        [FromRoute] int id,
        [FromServices] BuildModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.UnpublishAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
