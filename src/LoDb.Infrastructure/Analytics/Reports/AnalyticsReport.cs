namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>
/// The traffic and audience of a period, as the legacy admin's report
/// (<c>RangeReportBuilder</c>): what was viewed, when, by whom and from where.
/// </summary>
/// <remarks>
/// Breakdowns are sorted by count, then by name. Every hour, weekday and heatmap cell is in
/// UTC; the heatmap has a row per weekday, Monday first, of 24 hours each.
/// </remarks>
public sealed record AnalyticsReport
{
    /// <summary><c>7d</c>, <c>30d</c>, <c>90d</c> or <c>all</c>.</summary>
    public required string Range { get; init; }

    /// <summary>First day of the period, in UTC.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Last day of the period: today, in UTC.</summary>
    public required DateOnly To { get; init; }

    public required int Days { get; init; }

    public required AnalyticsTotals Totals { get; init; }

    /// <summary>Every day of the period, in order, those without a view included.</summary>
    public required IReadOnlyList<AnalyticsDay> Series { get; init; }

    /// <summary>Views by type: <c>home</c>, <c>champion</c>, <c>item</c>…</summary>
    public required IReadOnlyList<AnalyticsRank> ByType { get; init; }

    /// <summary>Views by kind of page: <c>home</c>, <c>list</c>, <c>detail</c>.</summary>
    public required IReadOnlyList<AnalyticsRank> ByKind { get; init; }

    /// <summary>Views by legacy route name, such as <c>app_champion</c>.</summary>
    public required IReadOnlyList<AnalyticsRank> ByRoute { get; init; }

    /// <summary>Views by HTTP status.</summary>
    public required IReadOnlyList<AnalyticsRank> Status { get; init; }

    /// <summary>The 20 most viewed paths.</summary>
    public required IReadOnlyList<AnalyticsRank> TopPages { get; init; }

    /// <summary>The 20 most viewed entities, keyed <c>{type}:{key}</c>.</summary>
    public required IReadOnlyList<AnalyticsRank> TopEntities { get; init; }

    /// <summary>Views by UTC hour, 24 values.</summary>
    public required IReadOnlyList<long> ByHour { get; init; }

    /// <summary>Views by UTC weekday, Monday first, 7 values.</summary>
    public required IReadOnlyList<long> ByWeekday { get; init; }

    /// <summary>Views by weekday (rows, Monday first) and UTC hour (columns).</summary>
    public required IReadOnlyList<IReadOnlyList<long>> Heatmap { get; init; }

    /// <summary>Views by interface locale.</summary>
    public required IReadOnlyList<AnalyticsRank> Locale { get; init; }

    /// <summary>Views by Data Dragon language.</summary>
    public required IReadOnlyList<AnalyticsRank> Lang { get; init; }

    public required IReadOnlyList<AnalyticsRank> Browser { get; init; }

    public required IReadOnlyList<AnalyticsRank> Os { get; init; }

    /// <summary>Views by device: <c>desktop</c>, <c>mobile</c>, <c>tablet</c>…</summary>
    public required IReadOnlyList<AnalyticsRank> Device { get; init; }

    /// <summary>
    /// Views by source: <c>direct</c>, <c>internal</c>, <c>search</c>, <c>social</c>,
    /// <c>external</c>.
    /// </summary>
    public required IReadOnlyList<AnalyticsRank> RefSource { get; init; }

    /// <summary>The 15 hosts that referred the most views, the site itself included.</summary>
    public required IReadOnlyList<AnalyticsRank> TopReferers { get; init; }

    /// <summary>Views by country, for the views that have one.</summary>
    public required IReadOnlyList<AnalyticsCountry> Country { get; init; }
}
