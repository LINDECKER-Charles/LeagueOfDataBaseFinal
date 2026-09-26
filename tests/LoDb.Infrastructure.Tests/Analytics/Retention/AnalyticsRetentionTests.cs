using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Retention;
using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Tests.Analytics.Retention;

/// <summary>
/// The daily upkeep on a simulated clock: the partitions of the coming week created ahead,
/// the addresses and user agents erased after 30 days, the views dropped after 13 months,
/// the daily aggregates kept.
/// </summary>
public sealed class AnalyticsRetentionTests(PostgresContainerFixture postgres)
    : AnalyticsDatabase(postgres)
{
    private IAnalyticsRetention Retention => Get<IAnalyticsRetention>();

    private IAnalyticsPartitions Partitions => Get<IAnalyticsPartitions>();

    [Fact]
    public async Task PartitionsExistFromYesterdayToAWeekAhead()
    {
        var summary = await Retention.ApplyAsync(Cancellation);

        Assert.Equal(9, summary.PartitionsCreated);
        Assert.Equal(
            Enumerable.Range(-1, 9).Select(Today.AddDays),
            await Partitions.ListAsync(Cancellation));

        Time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, (await Retention.ApplyAsync(Cancellation)).PartitionsCreated);
    }

    [Fact]
    public async Task ViewsAreDroppedWithTheirPartitionAfterThirteenMonths()
    {
        // 2025-08-26 is the first day kept on 2026-09-26.
        var firstKept = Today.AddMonths(-13);
        await WriteViewsAsync(At(9).AddMonths(-13).AddDays(-1), At(9).AddMonths(-13));

        var summary = await Retention.ApplyAsync(Cancellation);

        Assert.Equal(1, summary.PartitionsDropped);
        var kept = await Partitions.ListAsync(Cancellation);
        Assert.Equal(firstKept, kept[0]);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task AddressAndUserAgentAreErasedAfterThirtyDays()
    {
        await WriteViewsAsync(At(11).AddDays(-31), At(13).AddDays(-30), At(9));

        var summary = await Retention.ApplyAsync(Cancellation);

        Assert.Equal(1, summary.ClientDataErased);
        await using var context = Database.CreateContext();
        var views = await context.AnalyticsEvents.AsNoTracking()
            .OrderBy(static view => view.OccurredAt)
            .ToListAsync(Cancellation);
        Assert.Null(views[0].Ip);
        Assert.Null(views[0].UserAgent);
        Assert.All(views.Skip(1), static view => Assert.NotNull(view.Ip));
        Assert.All(views, static view => Assert.Equal(new string('c', 64), view.Visitor));
        Assert.Equal(0, (await Retention.ApplyAsync(Cancellation)).ClientDataErased);
    }

    [Fact]
    public async Task DailyAggregatesOutliveTheirViews()
    {
        var old = Today.AddYears(-3);
        var store = Get<DailyStore>();
        await store.AddImportedAsync(new DailyAggregate(old) { Views = 5 }, Cancellation);

        await Retention.ApplyAsync(Cancellation);

        Assert.Single(await store.ReadAsync(old, old, Cancellation));
    }

    private async Task<long> CountAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AnalyticsEvents.LongCountAsync(Cancellation);
    }
}
