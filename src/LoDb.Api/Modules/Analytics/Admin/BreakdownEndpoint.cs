using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Analytics.Reports.Breakdowns;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin;

/// <summary>
/// <c>GET /api/admin/analytics/breakdown?range=&amp;dimension=&amp;q=&amp;page=</c>: every
/// page, entity or referring host of a period, past the tops of the report, searched and 50
/// a page.
/// </summary>
internal static class BreakdownEndpoint
{
    /// <summary>The dimension is none of <see cref="AnalyticsDimensions.All"/>.</summary>
    public const string InvalidDimension = "invalid-dimension";

    private const string DimensionKey = "dimension";
    private const string PageKey = "page";
    private const string InvalidPage = "invalid-page";

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/breakdown", ReadAsync)
            .WithName("getAnalyticsBreakdown")
            .WithSummary("Every key of a breakdown of the audience over a period, by page.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    /// <param name="filters">The period, the dimension, the search and the page.</param>
    /// <param name="reports">The reports, read from the daily aggregates.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    private static async Task<Results<Ok<AnalyticsBreakdown>, AccountProblem>> ReadAsync(
        [AsParameters] BreakdownFilters filters,
        [FromServices] IAnalyticsReports reports,
        CancellationToken cancellationToken)
    {
        if (RangeParameter.Read(filters.Range) is not { } period)
        {
            return RangeParameter.Invalid();
        }

        if (ProblemOf(filters) is { } problem)
        {
            return problem;
        }

        var query = new AnalyticsBreakdownQuery
        {
            Dimension = filters.Dimension ?? AnalyticsDimensions.Pages,
            Search = SearchPattern.Normalize(filters.Q),
            Page = filters.Page ?? 1,
        };
        return TypedResults.Ok(await reports.BreakdownAsync(period, query, cancellationToken));
    }

    private static AccountProblem? ProblemOf(BreakdownFilters filters)
    {
        if (filters.Dimension is { } dimension
            && !AnalyticsDimensions.All.Contains(dimension, StringComparer.Ordinal))
        {
            return AdminProblems.Invalid(DimensionKey, InvalidDimension);
        }

        return filters.Page is < 1 ? AdminProblems.Invalid(PageKey, InvalidPage) : null;
    }
}
