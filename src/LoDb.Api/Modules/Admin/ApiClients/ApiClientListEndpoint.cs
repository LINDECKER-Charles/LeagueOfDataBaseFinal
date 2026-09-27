using LoDb.Api.Modules.Admin.ApiClients.Views;
using LoDb.Api.Modules.Admin.Http;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.ApiClients;

/// <summary>
/// <c>GET /api/admin/api-clients</c>: a page of the keys of the public API, newest first,
/// with the counters of the fleet and its heaviest users.
/// </summary>
internal static class ApiClientListEndpoint
{
    public static void Map(IEndpointRouteBuilder clients) =>
        clients.MapGet("/", ListAsync)
            .WithName("listAdminApiClients")
            .WithSummary("A page of the keys of the public API, newest first.");

    private static async Task<AdminApiClientPage> ListAsync(
        [FromQuery(Name = "page")] int? page,
        [FromServices] ApiClientDirectory directory,
        CancellationToken cancellationToken) =>
        await directory.ListAsync(AdminPaging.PageOf(page), cancellationToken);
}
