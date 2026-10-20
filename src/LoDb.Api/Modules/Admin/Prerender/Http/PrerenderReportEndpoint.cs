using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// <c>GET /api/admin/prerender</c>: whether the pre-rendering may run here, the epoch nginx
/// serves, the active pass with its pace and estimated end, the latest passes, and what the
/// store holds with its estimated monthly cost.
/// </summary>
internal static class PrerenderReportEndpoint
{
    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapGet(string.Empty, ReportAsync)
            .WithName("readAdminPrerender")
            .WithSummary("The passes of the pre-rendering and what the page store holds.");

    private static async Task<PrerenderReport> ReportAsync(
        [FromServices] PrerenderJournal journal,
        CancellationToken cancellationToken) =>
        await journal.ReportAsync(cancellationToken);
}
