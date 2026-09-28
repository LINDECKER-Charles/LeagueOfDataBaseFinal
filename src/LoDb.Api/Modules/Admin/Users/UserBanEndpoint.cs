using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Users;

/// <summary>
/// <c>POST /api/admin/users/{id}/ban</c>: bans an account, with an optional reason
/// (<c>admin.user_ban</c>); its sessions close at their next check.
/// </summary>
internal static class UserBanEndpoint
{
    public static void Map(IEndpointRouteBuilder users) =>
        users.MapPost("/{id:int}/ban", BanAsync)
            .WithName("banAdminUser")
            .WithSummary("Bans an account, which can no longer sign in.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<NoContent, AccountProblem>> BanAsync(
        [FromRoute] int id,
        [FromBody] BanRequest? request,
        [FromServices] UserModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.BanAsync(id, request?.Reason, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
