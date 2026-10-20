using LoDb.Api.Modules.Admin.Scaleway.Views;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// <c>GET /api/admin/scaleway/bill</c>: what Scaleway bills for the Object Storage of the
/// backups and of the page store, month by month, at most an hour old. Apart from the backups
/// and pre-rendering reports, so that they never wait on Scaleway.
/// </summary>
internal static class ScalewayBillEndpoint
{
    public static void Map(IEndpointRouteBuilder scaleway) =>
        scaleway.MapGet("/bill", ReadAsync)
            .WithName("readAdminScalewayBill")
            .WithSummary("The Object Storage bill of the backups and of the page store.");

    private static async Task<ScalewayBillReport> ReadAsync(
        [FromServices] ScalewayBillReader reader,
        CancellationToken cancellationToken) =>
        await reader.ReadAsync(cancellationToken);
}
