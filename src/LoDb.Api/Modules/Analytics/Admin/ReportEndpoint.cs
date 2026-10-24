using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Analytics.Reports;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin;

/// <summary>
/// <c>GET /api/admin/analytics/report?range=</c>: the audience report of the legacy admin
/// (traffic and audience panels) over the last 7, 30 or 90 days, or all the history.
/// </summary>
internal static class ReportEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/report", BuildAsync)
            .WithName("getAnalyticsReport")
            .WithSummary("The audience of the site over a period ending today (UTC).")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    /// <param name="range"><c>7d</c>, <c>30d</c> (by default), <c>90d</c> or <c>all</c>.</param>
    /// <param name="reports">The reports, read from the daily aggregates.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    private static async Task<Results<Ok<AnalyticsReport>, AccountProblem>> BuildAsync(
        [FromQuery] string? range,
        [FromServices] IAnalyticsReports reports,
        CancellationToken cancellationToken) =>
        RangeParameter.Read(range) is { } period
            ? TypedResults.Ok(await reports.BuildAsync(period, cancellationToken))
            : RangeParameter.Invalid();
}
