using LoDb.Api.Modules.Admin.Http;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Users;

/// <summary>
/// <c>GET /api/admin/users</c>: a page of the accounts whose username or e-mail holds the
/// search, newest first, with the counters of the moderation page.
/// </summary>
internal static class UserListEndpoint
{
    public static void Map(IEndpointRouteBuilder users) =>
        users.MapGet("/", SearchAsync)
            .WithName("searchAdminUsers")
            .WithSummary("A page of the accounts matching a search, newest first.");

    private static async Task<AdminUserPage> SearchAsync(
        [FromQuery(Name = "q")] string? search,
        [FromQuery(Name = "page")] int? page,
        [FromServices] UserDirectory directory,
        CancellationToken cancellationToken) =>
        await directory.SearchAsync(search, AdminPaging.PageOf(page), cancellationToken);
}
