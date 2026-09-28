using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.ApiClients;

/// <summary>
/// <c>POST /api/admin/api-clients/{id}/revoke</c>: revokes a key, refused by <c>/v1</c> from
/// its next request (<c>admin.api_client_revoke</c>).
/// </summary>
internal static class ApiClientRevokeEndpoint
{
    public static void Map(IEndpointRouteBuilder clients) =>
        clients.MapPost("/{id:int}/revoke", RevokeAsync)
            .WithName("revokeAdminApiClient")
            .WithSummary("Revokes a key of the public API, at once.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<NoContent, AccountProblem>> RevokeAsync(
        [FromRoute] int id,
        [FromServices] ApiClientDesk desk,
        CancellationToken cancellationToken)
    {
        var problem = await desk.RevokeAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
