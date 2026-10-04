using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Users;

/// <summary>
/// <c>POST /api/admin/users/{id}/unban</c>: lifts the ban of an account
/// (<c>admin.user_unban</c>).
/// </summary>
internal static class UserUnbanEndpoint
{
    public static void Map(IEndpointRouteBuilder users) =>
        users.MapPost("/{id:int}/unban", UnbanAsync)
            .WithName("unbanAdminUser")
            .WithSummary("Lifts the ban of an account.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, AccountProblem>> UnbanAsync(
        [FromRoute] int id,
        [FromServices] UserModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.UnbanAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
