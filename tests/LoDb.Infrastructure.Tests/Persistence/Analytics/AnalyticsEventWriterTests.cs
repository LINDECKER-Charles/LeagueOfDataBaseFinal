using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Analytics;

/// <summary>
/// The binary copy of a batch of views: every column as the model reads it but the client's
/// address and user agent, never written, text cut to its column, and nothing kept when one
/// view has no partition.
/// </summary>
public sealed class AnalyticsEventWriterTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private static readonly DateTimeOffset At =
        new DateTimeOffset(2026, 9, 26, 8, 30, 15, TimeSpan.Zero).AddTicks(1_234_560);

    private static readonly DateOnly Day = DateOnly.FromDateTime(At.UtcDateTime);

    private AnalyticsEventWriter Writer => new(Database.DataSource);

    [Fact]
    public async Task BatchIsReadBackAsWritten()
    {
        await CreatePartitionsAsync(Day, Day.AddDays(1));
        var views = new[]
        {
            AnalyticsSamples.View(At),
            Anonymous(AnalyticsSamples.View(At.ToOffset(TimeSpan.FromHours(2)).AddHours(1))),
        };

        await Writer.WriteAsync(views, Cancellation);

        var stored = await ReadAllAsync();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, static view => Assert.True(view.Id > 0));
        Assert.Equivalent(
            views.Select(static view => Normalized(view)),
            stored.Select(static view => Normalized(view)),
            strict: true);
    }

    [Fact]
    public async Task AddressAndUserAgentAreNeverWritten()
    {
        await CreatePartitionsAsync(Day, Day);
        var view = AnalyticsSamples.View(At);

        await Writer.WriteAsync([view], Cancellation);

        var stored = Assert.Single(await ReadAllAsync());
        Assert.Null(stored.Ip);
        Assert.Null(stored.UserAgent);
        Assert.Equal(view.Visitor, stored.Visitor);
        Assert.Equal((view.Browser, view.Os), (stored.Browser, stored.Os));
    }

    [Fact]
    public async Task LongTextIsCutToItsColumn()
    {
        await CreatePartitionsAsync(Day, Day);
        var view = AnalyticsSamples.View(At);
        view.RefererHost = new string('h', AnalyticsEvent.RefererHostMaxLength - 1) + "😀";
        view.Path = "/" + new string('p', 5_000);
        view.Country = "FRA";

        await Writer.WriteAsync([view], Cancellation);

        var stored = Assert.Single(await ReadAllAsync());
        Assert.Equal(new string('h', AnalyticsEvent.RefererHostMaxLength - 1), stored.RefererHost);
        Assert.Equal(AnalyticsEvent.PathMaxLength, stored.Path.Length);
        Assert.Equal("FR", stored.Country);
    }

    [Fact]
    public async Task ViewWithoutPartitionFailsTheWholeBatch()
    {
        await CreatePartitionsAsync(Day, Day);
        var views = new[] { AnalyticsSamples.View(At), AnalyticsSamples.View(At.AddDays(3)) };

        await Assert.ThrowsAsync<PostgresException>(() => Writer.WriteAsync(views, Cancellation));

        Assert.Empty(await ReadAllAsync());
    }

    private static AnalyticsEvent Anonymous(AnalyticsEvent view)
    {
        view.Origin = AnalyticsCaptureOrigin.ServedPage;
        view.Entity = null;
        view.Version = null;
        view.Lang = null;
        view.Ip = null;
        view.UserAgent = null;
        view.IsBot = true;
        view.RefererHost = null;
        view.Country = null;
        view.CountryName = null;
        return view;
    }

    // The id comes from the database, timestamptz reads back in UTC, and the client's
    // address and user agent are never written.
    private static AnalyticsEvent Normalized(AnalyticsEvent view)
    {
        view.Id = 0;
        view.OccurredAt = view.OccurredAt.ToUniversalTime();
        view.Ip = null;
        view.UserAgent = null;
        return view;
    }

    private async Task CreatePartitionsAsync(DateOnly first, DateOnly last) =>
        await new AnalyticsPartitions(Database.DataSource).CreateAsync(first, last, Cancellation);

    private async Task<List<AnalyticsEvent>> ReadAllAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AnalyticsEvents
            .AsNoTracking()
            .OrderBy(static view => view.OccurredAt)
            .ToListAsync(Cancellation);
    }
}
