using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>
/// <c>POST /api/admin/contacts/{id}/reopen</c>: puts a handled message back among the new ones.
/// </summary>
internal static class ContactReopenEndpoint
{
    public static void Map(IEndpointRouteBuilder contacts) =>
        contacts.MapPost("/{id:int}/reopen", ReopenAsync)
            .WithName("reopenAdminContact")
            .WithSummary("Puts a handled contact message back among the new ones.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, AccountProblem>> ReopenAsync(
        [FromRoute] int id,
        [FromServices] ContactModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.ReopenAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
