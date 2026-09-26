using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics.Reports;

/// <summary>
/// Reads the days of a period and builds its report. A day without an aggregate counts as
/// empty; <c>all</c> starts at the first day that has one.
/// </summary>
internal sealed class AnalyticsReports(
    DailyStore store,
    DayEvents events,
    TimeProvider timeProvider) : IAnalyticsReports
{
    public async Task<AnalyticsReport> BuildAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var first = await FirstDayAsync(range, today, cancellationToken);
        var rows = (await store.ReadAsync(first, today, cancellationToken))
            .ToDictionary(static row => row.Day);
        var dailies = new List<DailyAggregate>();
        for (var day = first; day < today; day = day.AddDays(1))
        {
            dailies.Add(rows.TryGetValue(day, out var row)
                ? DailyColumns.Read(row)
                : new DailyAggregate(day));
        }

        // An imported today is the legacy stack's count of it: it stays as imported.
        dailies.Add(rows.TryGetValue(today, out var imported)
            && imported.Source == AnalyticsDailySource.Import
                ? DailyColumns.Read(imported)
                : await events.FoldAsync(today, cancellationToken));
        return RangeReportBuilder.Build(dailies, range.Name);
    }

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
