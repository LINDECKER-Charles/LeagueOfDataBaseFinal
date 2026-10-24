using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Robots.Reports;
using LoDb.Infrastructure.Robots.Reports.Detail;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin.Robots;

/// <summary>
/// <c>GET /api/admin/analytics/robots/{key}?range=</c>: one robot over the period, its days,
/// user agents, networks, paths and latest hits.
/// </summary>
internal static class RobotDetailEndpoint
{
    /// <summary>No robot of this key was seen over the period.</summary>
    public const string RobotNotFound = "robot-not-found";

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/robots/{key}", DetailAsync)
            .WithName("getRobotDetail")
            .WithSummary("One robot of the site over a period ending today (UTC).")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    /// <param name="key">The robot's key, as the report lists it.</param>
    /// <param name="range"><c>7d</c>, <c>30d</c> (by default), <c>90d</c> or <c>all</c>.</param>
    /// <param name="reports">The reports of the robots journal.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    private static async Task<Results<Ok<RobotDetail>, AccountProblem>> DetailAsync(
        [FromRoute] string key,
        [FromQuery] string? range,
        [FromServices] IRobotReports reports,
        CancellationToken cancellationToken)
    {
        if (RangeParameter.Read(range) is not { } period)
        {
            return RangeParameter.Invalid();
        }

        return await reports.DetailAsync(key, period, cancellationToken) is { } detail
            ? TypedResults.Ok(detail)
            : AdminProblems.NotFound(RobotNotFound);
    }
}
