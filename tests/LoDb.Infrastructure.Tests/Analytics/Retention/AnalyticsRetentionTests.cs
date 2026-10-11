using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Retention;
using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Analytics.Retention;

/// <summary>
/// The daily upkeep on a simulated clock: the partitions of the coming week created ahead,
/// the addresses and user agents that views stored before the capture stopped keeping them
/// erased after 30 days, from the days next to the cutoff only, the views dropped after 13
/// months, the daily aggregates kept.
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
        await WriteStoredClientDataAsync(At(11).AddDays(-31), At(13).AddDays(-30), At(9));

        var summary = await Retention.ApplyAsync(Cancellation);

        Assert.Equal(1, summary.ClientDataErased);
        var views = await ReadViewsAsync();
        Assert.Null(views[0].Ip);
        Assert.Null(views[0].UserAgent);
        Assert.All(views.Skip(1), static view => Assert.NotNull(view.Ip));
        Assert.All(views, static view => Assert.Equal(new string('c', 64), view.Visitor));
        Assert.Equal(0, (await Retention.ApplyAsync(Cancellation)).ClientDataErased);
    }

    [Fact]
    public async Task ErasureReadsOnlyTheDaysNextToTheCutoff()
    {
        await WriteStoredClientDataAsync(At(9).AddDays(-40), At(9).AddDays(-31));

        Assert.Equal(1, (await Retention.ApplyAsync(Cancellation)).ClientDataErased);

        var views = await ReadViewsAsync();
        Assert.NotNull(views[0].Ip);
        Assert.Null(views[1].Ip);
    }

    // The job runs once a period at most: a host restarted before the next run is due waits
    // a whole period from its start (37 and 47 hours here), a failed run one more (71).
    [Theory]
    [InlineData(26)]
    [InlineData(37)]
    [InlineData(47)]
    [InlineData(71)]
    public async Task DelayedRunLeavesNoViewBehind(int hoursLater)
    {
        // Past the first run's cutoff (noon, 30 days ago), behind the next one's.
        await WriteStoredClientDataAsync(At(12, 30).AddDays(-30), At(23, 30).AddDays(-30));
        Assert.Equal(0, (await Retention.ApplyAsync(Cancellation)).ClientDataErased);

        Time.Advance(TimeSpan.FromHours(hoursLater));

        Assert.Equal(2, (await Retention.ApplyAsync(Cancellation)).ClientDataErased);
    }

    [Fact]
    public async Task ErasureReachesFurtherBackWhenAsked()
    {
        await WriteStoredClientDataAsync(At(9).AddDays(-40));
        await using var services = AnalyticsServices.Build(
            Database.AppConnectionString,
            Time,
            static options => options.ClientDataEraseDays = 15);

        var summary = await services.GetRequiredService<IAnalyticsRetention>()
            .ApplyAsync(Cancellation);

        Assert.Equal(1, summary.ClientDataErased);
    }

    [Fact]
    public async Task NewViewsHoldNoAddressNorUserAgent()
    {
        await WriteViewsAsync(At(9).AddDays(-31), At(9));

        Assert.Equal(0, (await Retention.ApplyAsync(Cancellation)).ClientDataErased);
        Assert.All(await ReadViewsAsync(), static view =>
        {
            Assert.Null(view.Ip);
            Assert.Null(view.UserAgent);
        });
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

    // Views as the capture stored them before it stopped keeping the client's data.
    private async Task WriteStoredClientDataAsync(params DateTimeOffset[] moments)
    {
        await WriteViewsAsync(moments);
        await Database.ExecuteAsync(
            "UPDATE analytics_event SET ip = '2001:db8::7', user_agent = 'Firefox/140.0'",
            Cancellation);
    }

    private async Task<List<AnalyticsEvent>> ReadViewsAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AnalyticsEvents.AsNoTracking()
            .OrderBy(static view => view.OccurredAt)
            .ToListAsync(Cancellation);
    }

    private async Task<long> CountAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AnalyticsEvents.LongCountAsync(Cancellation);
    }
}
