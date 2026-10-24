namespace LoDb.Infrastructure.Analytics.Reports.Breakdowns;

/// <summary>The counts of a dimension cut into the page a query asks for.</summary>
internal static class BreakdownPages
{
    public const int PageSize = 50;
    private const double Percent = 100;

    public static AnalyticsBreakdown Of(
        IReadOnlyDictionary<string, long> counts,
        AnalyticsBreakdownQuery query,
        string range)
    {
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(query);
        double total = counts.Values.Sum();
        var matched = counts
            .Where(entry => query.Search is not { } search
                || entry.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static entry => entry.Value)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToList();
        var page = Math.Max(1, query.Page);
        return new AnalyticsBreakdown
        {
            Range = range,
            Dimension = query.Dimension,
            Total = (long)total,
            Matched = matched.Count,
            Rows =
            [
                .. matched.Skip((page - 1) * PageSize).Take(PageSize).Select(entry =>
                    new AnalyticsRank
                    {
                        Name = entry.Key,
                        Count = entry.Value,
                        Pct = total > 0 ? entry.Value / total * Percent : 0,
                    }),
            ],
            Page = page,
            HasMore = matched.Count > page * PageSize,
        };
    }
}
