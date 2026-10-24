using LoDb.Infrastructure.Analytics.Reports;

namespace LoDb.Infrastructure.Robots.Reports.Views;

/// <summary>
/// The robots of a period ending today (UTC): who they are, what they asked and how they were
/// answered, day by day and robot by robot.
/// </summary>
/// <remarks>
/// Read from the daily counts, kept with no end. The paths and the hits themselves are kept
/// from <see cref="HitsSince"/> on only.
/// </remarks>
public sealed record RobotReport
{
    /// <summary><c>7d</c>, <c>30d</c>, <c>90d</c> or <c>all</c>.</summary>
    public required string Range { get; init; }

    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public required int Days { get; init; }

    /// <summary>The first day whose hits are still kept one by one.</summary>
    public required DateOnly HitsSince { get; init; }

    public required RobotTotals Totals { get; init; }

    /// <summary>Every day of the period, in order.</summary>
    public required IReadOnlyList<DateOnly> Dates { get; init; }

    /// <summary>The hits of each category, a value for each of <see cref="Dates"/>.</summary>
    public required IReadOnlyList<RobotSeries> CategorySeries { get; init; }

    public required IReadOnlyList<RobotCategoryShare> Categories { get; init; }

    /// <summary>Every robot of the period, the most active first.</summary>
    public required IReadOnlyList<RobotSummary> Robots { get; init; }

    /// <summary>Hits by status (<c>200</c>, <c>503</c>…).</summary>
    public required IReadOnlyList<AnalyticsRank> Statuses { get; init; }

    /// <summary>Hits by section (<c>detail</c>, <c>archive</c>, <c>sitemap</c>…).</summary>
    public required IReadOnlyList<AnalyticsRank> Sections { get; init; }

    /// <summary>Hits by verdict (<c>verified</c>, <c>mismatch</c>…).</summary>
    public required IReadOnlyList<AnalyticsRank> Verdicts { get; init; }

    /// <summary>The networks robots came from, the most active first, 25 at most.</summary>
    public required IReadOnlyList<RobotNetwork> Networks { get; init; }
}
