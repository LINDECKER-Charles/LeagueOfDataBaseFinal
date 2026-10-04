using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Analytics.Rollup;

/// <summary>
/// The rollup into <c>analytics_daily</c>: today continuously, a closed day once for all
/// after its close, idempotent, and never over an imported day.
/// </summary>
public sealed class AnalyticsRollupTests(PostgresContainerFixture postgres)
    : AnalyticsDatabase(postgres)
{
    private IAnalyticsRollup Rollup => Get<IAnalyticsRollup>();

    [Fact]
    public async Task TodayIsFoldedFromItsViews()
    {
        await WriteViewsAsync(At(9), At(10), At(11));

        Assert.Equal([Today], await Rollup.RollupTodayAsync(Cancellation));

        var row = Assert.Single(await Get<DailyStore>().ReadAsync(Today, Today, Cancellation));
        Assert.Equal(AnalyticsDailySource.Events, row.Source);
        Assert.Equal(3, DailyColumns.Read(row).Views);
    }

    [Fact]
    public async Task DayWithoutViewsGetsNoRow()
    {
        await Get<IAnalyticsPartitions>().CreateAsync(Today.AddDays(-2), Today, Cancellation);

        Assert.Empty(await Rollup.RollupTodayAsync(Cancellation));
        Assert.Empty(await Rollup.RollupClosedAsync(Cancellation));

        Assert.Empty(await Get<DailyStore>().ReadAsync(Today.AddDays(-2), Today, Cancellation));
    }

    [Fact]
    public async Task ClosedDayIsFoldedOnceAfterItsClose()
    {
        var day = Today;
        await WriteViewsAsync(At(23), At(23, 50));
        Time.SetUtcNow(At(23, 55));
        await Rollup.RollupTodayAsync(Cancellation);
        // Taken in at 23:59, written after midnight.
        await WriteViewsAsync(At(23, 59));

        Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal([day], await Rollup.RollupClosedAsync(Cancellation));
        Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal([day], await Rollup.RollupClosedAsync(Cancellation));
        Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Empty(await Rollup.RollupClosedAsync(Cancellation));

        var row = Assert.Single(await Get<DailyStore>().ReadAsync(day, day, Cancellation));
        Assert.Equal(3, DailyColumns.Read(row).Views);
    }

    [Fact]
    public async Task RollupAgainGivesTheSameAggregate()
    {
        await WriteViewsAsync(At(9), At(10));
        await Rollup.RollupTodayAsync(Cancellation);
        var first = Assert.Single(await Get<DailyStore>().ReadAsync(Today, Today, Cancellation));

        await Rollup.RollupTodayAsync(Cancellation);

        var again = Assert.Single(await Get<DailyStore>().ReadAsync(Today, Today, Cancellation));
        AggregateAssert.Equal(DailyColumns.Read(first), DailyColumns.Read(again));
    }

    [Fact]
    public async Task ImportedDayIsNeverReplaced()
    {
        var yesterday = Today.AddDays(-1);
        var store = Get<DailyStore>();
        await store.AddImportedAsync(new DailyAggregate(yesterday) { Views = 100 }, Cancellation);
        await store.AddImportedAsync(new DailyAggregate(Today) { Views = 200 }, Cancellation);
        await WriteViewsAsync(At(9).AddDays(-1), At(9));

        Assert.Empty(await Rollup.RollupClosedAsync(Cancellation));
        Assert.Empty(await Rollup.RollupTodayAsync(Cancellation));

        var rows = await store.ReadAsync(yesterday, Today, Cancellation);
        Assert.Equal(
            [(AnalyticsDailySource.Import, 100L), (AnalyticsDailySource.Import, 200L)],
            rows.Select(static row => (row.Source, DailyColumns.Read(row).Views)));
    }
}
