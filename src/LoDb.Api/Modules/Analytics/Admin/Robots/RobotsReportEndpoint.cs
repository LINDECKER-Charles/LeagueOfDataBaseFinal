using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Robots.Reports;
using LoDb.Infrastructure.Robots.Reports.Views;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin.Robots;

/// <summary>
/// <c>GET /api/admin/analytics/robots?range=</c>: every robot nginx answered over the period,
/// the hidden ones its pace gave away included: who they are, what they asked and how they
/// were answered.
/// </summary>
internal static class RobotsReportEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/robots", BuildAsync)
            .WithName("getRobotsReport")
            .WithSummary("The robots of the site over a period ending today (UTC).")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    /// <param name="range"><c>7d</c>, <c>30d</c> (by default), <c>90d</c> or <c>all</c>.</param>
    /// <param name="reports">The reports of the robots journal.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    private static async Task<Results<Ok<RobotReport>, AccountProblem>> BuildAsync(
        [FromQuery] string? range,
        [FromServices] IRobotReports reports,
        CancellationToken cancellationToken) =>
        RangeParameter.Read(range) is { } period
            ? TypedResults.Ok(await reports.BuildAsync(period, cancellationToken))
            : RangeParameter.Invalid();
}
