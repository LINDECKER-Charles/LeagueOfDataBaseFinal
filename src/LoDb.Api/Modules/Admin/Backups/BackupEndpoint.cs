using LoDb.Api.Modules.Admin.Backups.Views;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Backups;

/// <summary>
/// <c>GET /api/admin/backups</c>: the state of each database's off-host backups, the size of
/// the repository and its estimated monthly cost, then the steps of the last <c>days</c>.
/// </summary>
internal static class BackupEndpoint
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    public static void Map(IEndpointRouteBuilder admin) =>
        admin.MapGet("/backups", ReportAsync)
            .WithName("readAdminBackups")
            .WithSummary("The history of the off-host backups and their estimated cost.");

    /// <summary>The window asked for, 30 days when absent, within a day and a year.</summary>
    public static int DaysOf(int? days) => Math.Clamp(days ?? DefaultDays, 1, MaxDays);

    private static async Task<BackupReport> ReportAsync(
        [FromQuery(Name = "days")] int? days,
        [FromServices] BackupJournal journal,
        CancellationToken cancellationToken) =>
        await journal.ReportAsync(DaysOf(days), cancellationToken);
}
