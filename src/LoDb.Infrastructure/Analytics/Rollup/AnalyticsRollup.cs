using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;

namespace LoDb.Infrastructure.Analytics.Rollup;

/// <summary>
/// Rollup of the days that have a partition: a day without one holds no view to fold.
/// </summary>
/// <remarks>
/// A closed day is final once folded after its close plus <see cref="ClosingGrace"/>, the
/// time its last views take through the queue. A day without a single view gets no row: the
/// reports count it as empty.
/// </remarks>
internal sealed class AnalyticsRollup(
    DayEvents events,
    DailyStore store,
    IAnalyticsPartitions partitions,
    TimeProvider timeProvider) : IAnalyticsRollup
{
    /// <summary>After its close, how long a day may still receive views from the queue.</summary>
    public static readonly TimeSpan ClosingGrace = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<DateOnly>> RollupTodayAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return await FoldAsync(today, cancellationToken) ? [today] : [];
    }

    public async Task<IReadOnlyList<DateOnly>> RollupClosedAsync(
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var closed = (await partitions.ListAsync(cancellationToken))
            .Where(day => day < today)
            .ToList();
        if (closed.Count == 0)
        {
            return [];
        }

        var states = await store.StatesAsync(closed[0], closed[^1], cancellationToken);
        var written = new List<DateOnly>();
        foreach (var day in closed.Where(day => IsStale(states.GetValueOrDefault(day), day)))
        {
            if (await FoldAsync(day, cancellationToken))
            {
                written.Add(day);
            }
        }

        return written;
    }

    private static bool IsStale(DailyState? state, DateOnly day) =>
        state is null
        || (state.Source == AnalyticsDailySource.Events
            && state.UpdatedAt < AnalyticsPartitionNames.StartOf(day.AddDays(1)) + ClosingGrace);

    private async Task<bool> FoldAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var daily = await events.FoldAsync(day, cancellationToken);
        return !daily.IsEmpty && await store.UpsertEventsAsync(daily, cancellationToken);
    }
}
