using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Robots.Reports.Reading;
using LoDb.Infrastructure.Robots.Reports.Views;

namespace LoDb.Infrastructure.Robots.Reports.Folding;

/// <summary>The days of a period as the lines of a chart, a value for every date.</summary>
internal static class RobotSeriesFold
{
    /// <summary>A line per category that has a hit, in the order of the categories.</summary>
    public static IReadOnlyList<RobotSeries> ByCategory(
        IReadOnlyList<SeriesCell> cells,
        AgentMap map,
        DatePeriod period)
    {
        var byCategory = cells.ToLookup(
            cell => map.ProfileOf(cell.AgentId, cell.Detection, cell.Network).Category,
            StringComparer.Ordinal);
        return
        [
            .. RobotCategories.All
                .Where(byCategory.Contains)
                .Select(category => LineOf(category, byCategory[category], period)),
        ];
    }

    /// <summary>A line per class of statuses, <c>2xx</c> to <c>5xx</c>, always all four.</summary>
    public static IReadOnlyList<RobotSeries> ByStatusClass(
        IReadOnlyList<SeriesCell> cells,
        DatePeriod period) =>
    [
        .. StatusClasses.Classes.Select(statusClass => LineOf(
            StatusClasses.NameOf(statusClass),
            cells.Where(cell => cell.StatusClass == statusClass),
            period)),
    ];

    private static RobotSeries LineOf(
        string name,
        IEnumerable<SeriesCell> cells,
        DatePeriod period)
    {
        var values = new long[period.Days];
        foreach (var cell in cells)
        {
            var index = cell.Day.DayNumber - period.From.DayNumber;
            if (index >= 0 && index < values.Length)
            {
                values[index] += cell.Hits;
            }
        }

        return new RobotSeries { Name = name, Values = values };
    }
}
