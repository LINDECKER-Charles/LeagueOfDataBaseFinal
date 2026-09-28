using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>
/// <c>POST /api/admin/contacts/{id}/handle</c>: marks a message of the contact form handled.
/// </summary>
internal static class ContactHandleEndpoint
{
    public static void Map(IEndpointRouteBuilder contacts) =>
        contacts.MapPost("/{id:int}/handle", HandleAsync)
            .WithName("handleAdminContact")
            .WithSummary("Marks a contact message handled.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, AccountProblem>> HandleAsync(
        [FromRoute] int id,
        [FromServices] ContactModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.HandleAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
