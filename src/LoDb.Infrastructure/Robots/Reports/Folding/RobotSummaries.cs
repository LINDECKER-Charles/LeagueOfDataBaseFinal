using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Robots.Reports.Reading;
using LoDb.Infrastructure.Robots.Reports.Views;

namespace LoDb.Infrastructure.Robots.Reports.Folding;

/// <summary>The cells of a period folded robot by robot, the most active first.</summary>
internal static class RobotSummaries
{
    private const int TopNetworks = 3;

    public static IReadOnlyList<RobotSummary> Of(IReadOnlyList<DailyCell> cells, AgentMap map)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(map);
        double total = cells.Sum(static cell => cell.Hits);
        return
        [
            .. cells
                .Select(cell =>
                    (Profile: map.ProfileOf(cell.AgentId, cell.Detection, cell.Network), cell))
                .GroupBy(static entry => entry.Profile.Key, StringComparer.Ordinal)
                .Select(group => SummaryOf(
                    group.First().Profile,
                    [.. group.Select(static entry => entry.cell)],
                    total))
                .OrderByDescending(static summary => summary.Figures.Hits)
                .ThenBy(static summary => summary.Key, StringComparer.Ordinal),
        ];
    }

    /// <param name="profile">The robot.</param>
    /// <param name="cells">Its cells, at least one.</param>
    /// <param name="total">The hits of every robot, for its share.</param>
    public static RobotSummary SummaryOf(
        RobotProfile profile,
        IReadOnlyList<DailyCell> cells,
        double total)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var figures = RobotFiguresFold.Of(cells);
        return new RobotSummary
        {
            Key = profile.Key,
            Name = profile.Name,
            Operator = profile.Operator,
            Category = profile.Category,
            IsCrawler = RobotCategories.IsCrawler(profile.Category),
            Pct = Shares.Of(figures.Hits, total),
            Figures = figures,
            FirstDay = cells.Min(static cell => cell.FirstDay),
            LastDay = cells.Max(static cell => cell.LastDay),
            TopSection = TopOf(cells, static cell => cell.Section).First(),
            Networks =
            [
                .. TopOf(cells, static cell => cell.Network)
                    .Where(static network => network.Length > 0)
                    .Take(TopNetworks),
            ],
        };
    }

    // The values of a dimension, the most hit first.
    private static IEnumerable<string> TopOf(
        IEnumerable<DailyCell> cells,
        Func<DailyCell, string> dimension) =>
        cells.GroupBy(dimension, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Sum(static cell => cell.Hits))
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => group.Key);
}
