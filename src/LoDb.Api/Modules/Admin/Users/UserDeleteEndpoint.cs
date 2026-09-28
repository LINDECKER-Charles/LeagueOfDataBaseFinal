using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Users;

/// <summary>
/// <c>DELETE /api/admin/users/{id}</c>: erases an account with its builds, votes and API
/// keys (<c>admin.user_delete</c>); donations and messages lose their link.
/// </summary>
internal static class UserDeleteEndpoint
{
    public static void Map(IEndpointRouteBuilder users) =>
        users.MapDelete("/{id:int}", DeleteAsync)
            .WithName("deleteAdminUser")
            .WithSummary("Erases an account, its builds, votes and API keys.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<NoContent, AccountProblem>> DeleteAsync(
        [FromRoute] int id,
        [FromServices] UserModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.DeleteAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
