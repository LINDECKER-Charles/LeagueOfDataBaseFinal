using LoDb.Infrastructure.Robots.Reports.Reading;
using LoDb.Infrastructure.Robots.Reports.Views;
using LoDb.Infrastructure.Robots.Verification;

namespace LoDb.Infrastructure.Robots.Reports.Folding;

/// <summary>The figures of a set of cells: one robot's, or a whole period's.</summary>
internal static class RobotFiguresFold
{
    public static RobotFigures Of(IReadOnlyCollection<DailyCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var hits = cells.Sum(static cell => cell.Hits);
        return new RobotFigures
        {
            Hits = hits,
            Verified = SumWhere(cells, static cell => cell.Verdict == RobotVerdicts.Verified),
            Mismatch = SumWhere(cells, static cell => cell.Verdict == RobotVerdicts.Mismatch),
            Unresolved = SumWhere(cells, static cell => cell.Verdict == RobotVerdicts.Unresolved),
            Success = SumOfClass(cells, StatusClasses.Success),
            Redirects = SumOfClass(cells, StatusClasses.Redirect),
            ClientErrors = SumOfClass(cells, StatusClasses.ClientError),
            ServerErrors = SumOfClass(cells, StatusClasses.ServerError),
            Refused = SumWhere(cells, static cell => StatusClasses.IsRefusal(cell.Status)),
            Bytes = cells.Sum(static cell => cell.Bytes),
            MeanMs = Shares.Mean(cells.Sum(static cell => cell.DurationMs), hits),
            SlowHits = cells.Sum(static cell => cell.SlowHits),
        };
    }

    private static long SumWhere(IEnumerable<DailyCell> cells, Func<DailyCell, bool> predicate) =>
        cells.Where(predicate).Sum(static cell => cell.Hits);

    private static long SumOfClass(IEnumerable<DailyCell> cells, int statusClass) =>
        SumWhere(cells, cell => StatusClasses.ClassOf(cell.Status) == statusClass);
}
