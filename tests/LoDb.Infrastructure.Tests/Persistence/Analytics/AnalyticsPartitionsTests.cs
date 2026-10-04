using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Analytics;

/// <summary>
/// The daily partitions of <c>analytics_event</c>: created once each, bounded by the UTC day,
/// dropped with their events, and required before a view of their day is kept.
/// </summary>
public sealed class AnalyticsPartitionsTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private static readonly DateOnly Day = new(2026, 9, 26);

    private AnalyticsPartitions Partitions => new(Database.DataSource);

    [Fact]
    public async Task MigrationCreatesNoPartition()
    {
        Assert.Empty(await Partitions.ListAsync(Cancellation));
        Assert.Equal(
            ["p"],
            await Database.QueryAsync(
                "SELECT relkind::text FROM pg_class WHERE relname = 'analytics_event'",
                Cancellation));
    }

    [Fact]
    public async Task CreateMakesOnePartitionPerDayOnce()
    {
        var created = await Partitions.CreateAsync(Day, Day.AddDays(2), Cancellation);
        var again = await Partitions.CreateAsync(Day.AddDays(1), Day.AddDays(3), Cancellation);

        Assert.Equal(3, created);
        Assert.Equal(1, again);
        Assert.Equal(
            [Day, Day.AddDays(1), Day.AddDays(2), Day.AddDays(3)],
            await Partitions.ListAsync(Cancellation));
        Assert.Contains("analytics_event_20260926", await Database.TablesAsync(Cancellation));
    }

    [Fact]
    public async Task PartitionHoldsItsUtcDayOnly()
    {
        await Partitions.CreateAsync(Day, Day.AddDays(1), Cancellation);

        await InsertAtAsync("2026-09-26T00:00:00Z");
        await InsertAtAsync("2026-09-26T23:59:59.999999Z");
        // 23:30 UTC on the 26th.
        await InsertAtAsync("2026-09-27T01:30:00+02:00");
        await InsertAtAsync("2026-09-27T00:00:00Z");

        Assert.Equal(
            ["analytics_event_20260926 3", "analytics_event_20260927 1"],
            await Database.QueryAsync(
                """
                SELECT tableoid::regclass || ' ' || count(*) FROM analytics_event
                GROUP BY tableoid ORDER BY 1
                """,
                Cancellation));
    }

    [Fact]
    public async Task ViewWithoutPartitionIsRefused()
    {
        await Partitions.CreateAsync(Day, Day, Cancellation);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAtAsync("2026-09-27T00:00:00Z"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Contains("no partition", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DropBeforeRemovesOlderDaysWithTheirEvents()
    {
        await Partitions.CreateAsync(Day, Day.AddDays(2), Cancellation);
        await InsertAtAsync("2026-09-26T12:00:00Z");
        await InsertAtAsync("2026-09-28T12:00:00Z");

        var dropped = await Partitions.DropBeforeAsync(Day.AddDays(2), Cancellation);
        var again = await Partitions.DropBeforeAsync(Day.AddDays(2), Cancellation);

        Assert.Equal(2, dropped);
        Assert.Equal(0, again);
        Assert.Equal([Day.AddDays(2)], await Partitions.ListAsync(Cancellation));
        Assert.Equal(
            ["1"],
            await Database.QueryAsync("SELECT count(*) FROM analytics_event", Cancellation));
        Assert.DoesNotContain("analytics_event_20260926", await Database.TablesAsync(Cancellation));
    }

    [Fact]
    public async Task ConcurrentCreationsMakeEachPartitionOnce()
    {
        var counts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            new AnalyticsPartitions(Database.DataSource).CreateAsync(
                Day,
                Day.AddDays(6),
                Cancellation)));

        Assert.Equal(7, counts.Sum());
        Assert.Equal(7, (await Partitions.ListAsync(Cancellation)).Count);
    }

    [Fact]
    public async Task ListIgnoresOtherTables()
    {
        await Database.ExecuteAsync(
            "CREATE TABLE analytics_event_archive (id integer); CREATE TABLE analytics_event_1 ()",
            Cancellation);
        await Partitions.CreateAsync(Day, Day, Cancellation);

        Assert.Equal([Day], await Partitions.ListAsync(Cancellation));
    }

    [Fact]
    public async Task ReversedRangeIsRejected() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Partitions.CreateAsync(Day, Day.AddDays(-1), Cancellation));

    [Theory]
    [InlineData("analytics_event_20260926", true)]
    [InlineData("analytics_event_20260230", false)]
    [InlineData("analytics_event_2026092", false)]
    [InlineData("analytics_event_archive", false)]
    [InlineData("analytics_daily", false)]
    public void PartitionNamesRoundTrip(string name, bool isPartition)
    {
        Assert.Equal(isPartition, AnalyticsPartitionNames.TryParseDay(name, out var day));
        if (isPartition)
        {
            Assert.Equal(name, AnalyticsPartitionNames.Of(day!.Value));
        }
    }

    private Task InsertAtAsync(string occurredAt) =>
        Database.ExecuteAsync(
            $"""
            INSERT INTO analytics_event (occurred_at, origin, route, path, type, kind, status,
                locale, visitor, browser, os, device, is_bot, referer_source)
            VALUES ('{occurredAt}', 'page', 'home', '/en', 'home', 'home', 200, 'en', 'v',
                'other', 'other', 'other', false, 'direct')
            """,
            Cancellation);
}
