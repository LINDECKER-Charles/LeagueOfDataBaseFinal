using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>
/// <c>DELETE /api/admin/contacts/{id}</c>: deletes a message of the contact form.
/// </summary>
internal static class ContactDeleteEndpoint
{
    public static void Map(IEndpointRouteBuilder contacts) =>
        contacts.MapDelete("/{id:int}", DeleteAsync)
            .WithName("deleteAdminContact")
            .WithSummary("Deletes a contact message.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, AccountProblem>> DeleteAsync(
        [FromRoute] int id,
        [FromServices] ContactModeration moderation,
        CancellationToken cancellationToken)
    {
        var problem = await moderation.DeleteAsync(id, cancellationToken);
        return problem is null ? TypedResults.NoContent() : problem;
    }
}
