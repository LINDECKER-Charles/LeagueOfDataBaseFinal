using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Robots.Detection;
using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Robots.Reports;
using LoDb.Infrastructure.Robots.Reports.Journal;
using LoDb.Infrastructure.Robots.Verification;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Analytics.Admin.Robots;

/// <summary>
/// <c>GET /api/admin/analytics/robot-hits</c>: the hits of robots still kept (30 days), newest
/// first, 50 a page, filtered by robot, category, class of status, verdict, section and a text
/// of the path.
/// </summary>
internal static class RobotHitsEndpoint
{
    /// <summary>A filter holds a value the journal does not know.</summary>
    public const string InvalidFilter = "invalid-filter";

    private const int MinStatusClass = 1;
    private const int MaxStatusClass = 5;

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/robot-hits", ReadAsync)
            .WithName("getRobotHits")
            .WithSummary("A page of the hits of robots, newest first.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    /// <param name="filters">The filters of the page, all optional.</param>
    /// <param name="reports">The reports of the robots journal.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    private static async Task<Results<Ok<RobotHitPage>, AccountProblem>> ReadAsync(
        [AsParameters] RobotHitFilters filters,
        [FromServices] IRobotReports reports,
        CancellationToken cancellationToken)
    {
        if (InvalidFieldOf(filters) is { } field)
        {
            return AdminProblems.Invalid(field, InvalidFilter);
        }

        var query = new RobotHitQuery
        {
            // Every key is lowercase: a robot typed "Googlebot" is googlebot.
            Robot = Blank(filters.Robot)?.ToLowerInvariant(),
            Category = Blank(filters.Category),
            StatusClass = filters.Status,
            Verdict = Blank(filters.Verdict),
            Section = Blank(filters.Section),
            TargetPattern = SearchPattern.Normalize(filters.Q) is { } text
                ? SearchPattern.Containing(text)
                : null,
            Page = filters.Page ?? 1,
        };
        return TypedResults.Ok(await reports.HitsAsync(query, cancellationToken));
    }

    // The name of the first filter that holds an unknown value; null when all are known.
    private static string? InvalidFieldOf(RobotHitFilters filters) =>
        IsUnknown(filters.Category, RobotCategories.All) ? "category"
        : IsUnknown(filters.Verdict, RobotVerdicts.All) ? "verdict"
        : IsUnknown(filters.Section, HitSections.All) ? "section"
        : filters.Status is < MinStatusClass or > MaxStatusClass ? "status"
        : filters.Page is < 1 ? "page"
        : null;

    private static bool IsUnknown(string? value, IReadOnlyList<string> known) =>
        Blank(value) is { } text && !known.Contains(text, StringComparer.Ordinal);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
