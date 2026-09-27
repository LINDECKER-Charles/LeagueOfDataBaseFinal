using LoDb.Api.Modules.Admin.Http;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Donations;

/// <summary>
/// <c>GET /api/admin/donations</c>: a page of the donations, newest first, with their
/// counters and the daily totals of the last thirty days.
/// </summary>
internal static class DonationListEndpoint
{
    public static void Map(IEndpointRouteBuilder donations) =>
        donations.MapGet("/", ListAsync)
            .WithName("listAdminDonations")
            .WithSummary("A page of the donations, newest first, with their counters.");

    private static async Task<AdminDonationPage> ListAsync(
        [FromQuery(Name = "page")] int? page,
        [FromServices] DonationLedger ledger,
        CancellationToken cancellationToken) =>
        await ledger.ListAsync(AdminPaging.PageOf(page), cancellationToken);
}
