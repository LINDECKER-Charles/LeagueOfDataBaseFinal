using LoDb.Api.Modules.Admin.Monitoring.Views;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Monitoring;

/// <summary>
/// <c>GET /api/admin/monitoring</c>: the probes of the dependencies, the queues, the Data
/// Dragon versions, the counters of the application and the figures of the process, at most
/// thirty seconds old unless <c>refresh</c> asks for a new report.
/// </summary>
internal static class MonitoringEndpoint
{
    public static void Map(IEndpointRouteBuilder admin) =>
        admin.MapGet("/monitoring", ReportAsync)
            .WithName("readAdminMonitoring")
            .WithSummary("The state of the API, its dependencies and its queues.");

    private static async Task<MonitoringReport> ReportAsync(
        [FromQuery(Name = "refresh")] bool? refresh,
        [FromServices] MonitoringReporter reporter,
        CancellationToken cancellationToken) =>
        await reporter.ReportAsync(refresh == true, cancellationToken);
}
