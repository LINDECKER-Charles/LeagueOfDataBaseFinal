using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Robots.Reports.Journal;
using LoDb.Infrastructure.Robots.Reports.Views;

namespace LoDb.Infrastructure.Robots.Reports.Detail;

/// <summary>
/// One robot over a period: its figures, its days by class of status, what it asked, from
/// which networks and with which user agents, and its latest hits.
/// </summary>
public sealed record RobotDetail
{
    public required string Range { get; init; }

    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    /// <summary>
    /// The first day whose hits are still kept one by one: the paths start there.
    /// </summary>
    public required DateOnly HitsSince { get; init; }

    public required RobotSummary Robot { get; init; }

    /// <summary>
    /// The domains its genuine addresses reverse to; none when it cannot be checked.
    /// </summary>
    public required IReadOnlyList<string> VerifyDomains { get; init; }

    public required IReadOnlyList<DateOnly> Dates { get; init; }

    /// <summary><c>2xx</c> to <c>5xx</c>, a value for each of <see cref="Dates"/>.</summary>
    public required IReadOnlyList<RobotSeries> StatusSeries { get; init; }

    public required IReadOnlyList<AnalyticsRank> Statuses { get; init; }

    public required IReadOnlyList<AnalyticsRank> Sections { get; init; }

    public required IReadOnlyList<AnalyticsRank> Verdicts { get; init; }

    public required IReadOnlyList<RobotNetwork> Networks { get; init; }

    /// <summary>The user agents it sent, the most frequent first.</summary>
    public required IReadOnlyList<RobotAgentView> Agents { get; init; }

    /// <summary>The paths it asked the most since <see cref="HitsSince"/>, 50 at most.</summary>
    public required IReadOnlyList<RobotPathView> Paths { get; init; }

    /// <summary>Its latest hits, newest first.</summary>
    public required IReadOnlyList<RobotHitView> Recent { get; init; }
}
