using LoDb.Api.Modules.Admin.Http;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>
/// <c>GET /api/admin/contacts</c>: a page of the contact messages, new, handled or both,
/// newest first, with the counters of the inbox.
/// </summary>
internal static class ContactListEndpoint
{
    public static void Map(IEndpointRouteBuilder contacts) =>
        contacts.MapGet("/", ListAsync)
            .WithName("listAdminContacts")
            .WithSummary("A page of the contact messages, newest first.");

    private static async Task<AdminContactPage> ListAsync(
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "page")] int? page,
        [FromServices] ContactInbox inbox,
        CancellationToken cancellationToken) =>
        await inbox.ListAsync(status, AdminPaging.PageOf(page), cancellationToken);
}
