using LoDb.Api.Modules.Admin.Storage.Views;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Storage;

/// <summary>
/// <c>GET /api/admin/storage</c>: the objects of the storage root by family, the images and
/// their WebP siblings, the datasets and the savings of the content addressing, at most ten
/// minutes old unless <c>refresh</c> asks for a new report.
/// </summary>
internal static class StorageEndpoint
{
    public static void Map(IEndpointRouteBuilder admin) =>
        admin.MapGet("/storage", ReportAsync)
            .WithName("readAdminStorage")
            .WithSummary("What the storage root holds, by family, image format and dataset.");

    private static async Task<StorageReport> ReportAsync(
        [FromQuery(Name = "refresh")] bool? refresh,
        [FromServices] StorageReporter reporter,
        CancellationToken cancellationToken) =>
        await reporter.ReportAsync(refresh == true, cancellationToken);
}
