using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Geo;
using LoDb.Infrastructure.Analytics.Reports.Breakdowns;
using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>
/// Reads the days of a period and builds its report. A day without an aggregate counts as
/// empty; <c>all</c> starts at the first day that has one.
/// </summary>
/// <remarks>
/// The period of the same length just before gives the report its previous totals, read
/// from the aggregates alone: it ends before today.
/// </remarks>
internal sealed class AnalyticsReports(
    DailyStore store,
    DayEvents events,
    IGeoLocator geo,
    TimeProvider timeProvider) : IAnalyticsReports
{
    public async Task<AnalyticsReport> BuildAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        var today = Today();
        var first = await FirstDayAsync(range, today, cancellationToken);
        var dailies = await DailiesAsync(first, today, cancellationToken);
        var report = RangeReportBuilder.Build(dailies, range.Name, geo.IsAvailable);
        if (range.Days is not { } span)
        {
            return report;
        }

        var before = await ClosedDailiesAsync(first.AddDays(-span), first, cancellationToken);
        return report with { Previous = RangeReportBuilder.Totals(before) };
    }

    public async Task<AnalyticsBreakdown> BreakdownAsync(
        AnalyticsRange range,
        AnalyticsBreakdownQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(query);
        var today = Today();
        var first = await FirstDayAsync(range, today, cancellationToken);
        var dailies = await DailiesAsync(first, today, cancellationToken);
        var counts = RangeReportBuilder.Counts(
            dailies,
            AnalyticsDimensions.BucketOf(query.Dimension));
        return BreakdownPages.Of(counts, query, range.Name);
    }

    // Every day from the first to today, today folded from its views.
    private async Task<List<DailyAggregate>> DailiesAsync(
        DateOnly first,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var rows = (await store.ReadAsync(first, today, cancellationToken))
            .ToDictionary(static row => row.Day);
        var dailies = DaysOf(rows, first, today);

        // An imported today is the legacy stack's count of it: it stays as imported.
        dailies.Add(rows.TryGetValue(today, out var imported)
            && imported.Source == AnalyticsDailySource.Import
                ? DailyColumns.Read(imported)
                : await events.FoldAsync(today, cancellationToken));
        return dailies;
    }

    // Every day from the first to the day before the end, read from their aggregates.
    private async Task<List<DailyAggregate>> ClosedDailiesAsync(
        DateOnly first,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var rows = (await store.ReadAsync(first, end.AddDays(-1), cancellationToken))
            .ToDictionary(static row => row.Day);
        return DaysOf(rows, first, end);
    }

    private static List<DailyAggregate> DaysOf(
        Dictionary<DateOnly, DailyRow> rows,
        DateOnly first,
        DateOnly end)
    {
        var dailies = new List<DailyAggregate>();
        for (var day = first; day < end; day = day.AddDays(1))
        {
            dailies.Add(rows.TryGetValue(day, out var row)
                ? DailyColumns.Read(row)
                : new DailyAggregate(day));
        }

        return dailies;
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private async Task<DateOnly> FirstDayAsync(
        AnalyticsRange range,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (range.Days is { } span)
        {
            return today.AddDays(1 - span);
        }

        return await store.EarliestAsync(cancellationToken) is { } earliest && earliest < today
            ? earliest
            : today;
    }
}
