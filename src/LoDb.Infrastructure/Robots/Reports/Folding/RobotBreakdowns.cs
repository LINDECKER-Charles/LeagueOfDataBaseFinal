using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Robots.Reports.Reading;
using LoDb.Infrastructure.Robots.Reports.Views;

namespace LoDb.Infrastructure.Robots.Reports.Folding;

/// <summary>The totals and breakdowns of a period, from its cells and its robots.</summary>
internal static class RobotBreakdowns
{
    private const int TopNetworks = 25;

    public static RobotTotals TotalsOf(
        IReadOnlyList<DailyCell> cells,
        IReadOnlyList<RobotSummary> robots) => new()
    {
        Figures = RobotFiguresFold.Of(cells),
        Robots = robots.Count,
        CrawlerHits = robots.Where(static robot => robot.IsCrawler)
            .Sum(static robot => robot.Figures.Hits),
    };

    /// <summary>The hits by the value of a dimension, the most hit first.</summary>
    public static IReadOnlyList<AnalyticsRank> Rank(
        IReadOnlyList<DailyCell> cells,
        Func<DailyCell, string> dimension)
    {
        double total = cells.Sum(static cell => cell.Hits);
        return
        [
            .. cells.GroupBy(dimension, StringComparer.Ordinal)
                .Select(group => (group.Key, Count: group.Sum(static cell => cell.Hits)))
                .OrderByDescending(static entry => entry.Count)
                .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new AnalyticsRank
                {
                    Name = entry.Key,
                    Count = entry.Count,
                    Pct = Shares.Of(entry.Count, total),
                }),
        ];
    }

    public static IReadOnlyList<RobotCategoryShare> CategoriesOf(
        IReadOnlyList<RobotSummary> robots)
    {
        double total = robots.Sum(static robot => robot.Figures.Hits);
        return
        [
            .. robots.GroupBy(static robot => robot.Category, StringComparer.Ordinal)
                .Select(group => new RobotCategoryShare
                {
                    Category = group.Key,
                    Hits = group.Sum(static robot => robot.Figures.Hits),
                    Robots = group.Count(),
                    Pct = Shares.Of(group.Sum(static robot => robot.Figures.Hits), total),
                })
                .OrderByDescending(static share => share.Hits)
                .ThenBy(static share => share.Category, StringComparer.Ordinal),
        ];
    }

    /// <summary>The networks hits came from, the hits without one aside.</summary>
    public static IReadOnlyList<RobotNetwork> NetworksOf(
        IReadOnlyList<DailyCell> cells,
        AgentMap map)
    {
        double total = cells.Sum(static cell => cell.Hits);
        return
        [
            .. cells.Where(static cell => cell.Network.Length > 0)
                .GroupBy(static cell => cell.Network, StringComparer.Ordinal)
                .Select(group => new RobotNetwork
                {
                    Domain = group.Key,
                    Hits = group.Sum(static cell => cell.Hits),
                    Robots = RobotsIn(group, map),
                    Pct = Shares.Of(group.Sum(static cell => cell.Hits), total),
                })
                .OrderByDescending(static network => network.Hits)
                .ThenBy(static network => network.Domain, StringComparer.Ordinal)
                .Take(TopNetworks),
        ];
    }

    private static int RobotsIn(IEnumerable<DailyCell> cells, AgentMap map) =>
        cells.Select(cell => map.ProfileOf(cell.AgentId, cell.Detection, cell.Network).Key)
            .Distinct(StringComparer.Ordinal)
            .Count();
}
