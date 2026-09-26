using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Persistence.Apps;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Infrastructure.Tests.Persistence.Analytics;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Tests.Persistence.Billing;

/// <summary>
/// Every entity of lots 6, 7, 9 and 10 comes back as it was saved, every column filled, and
/// enumerations are stored with the spelling the raw SQL uses.
/// </summary>
public sealed class Lot6EntityRoundTripTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    // UTC and whole microseconds: what timestamptz keeps.
    private static readonly DateTimeOffset At =
        new DateTimeOffset(2026, 9, 26, 8, 30, 15, TimeSpan.Zero).AddTicks(1_234_560);

    [Fact]
    public async Task StripeEventRoundTrips()
    {
        await AssertRoundTripsAsync(new StripeEvent
        {
            Id = "evt_1Q2w3E4r5T6y7U8i",
            Type = "checkout.session.completed",
            Status = StripeEventStatus.Ignored,
            CreatedAt = At.AddSeconds(-3),
            ProcessedAt = At,
        });
        Assert.Equal(
            ["ignored"],
            await Database.QueryAsync("SELECT status FROM stripe_event", Cancellation));
    }

    [Fact]
    public async Task ApiCreditGrantRoundTrips()
    {
        var key = await SeedApiKeyAsync();

        await AssertRoundTripsAsync(new ApiCreditGrant
        {
            ApiKeyId = key,
            Source = ApiCreditGrantSource.Purchase,
            Requests = 20_000,
            PurchasedAt = At,
            ExpiresAt = At.AddMonths(ApiCreditGrant.ValidityMonths),
            StripeSessionId = "cs_test_a1B2c3D4",
            ExpiredAt = At.AddMonths(ApiCreditGrant.ValidityMonths).AddHours(1),
            ExpiredRequests = 1_234,
        });
        Assert.Equal(
            ["purchase"],
            await Database.QueryAsync("SELECT source FROM api_credit_grants", Cancellation));
    }

    [Fact]
    public async Task AnalyticsEventRoundTrips()
    {
        await PartitionsOf(DateOnly.FromDateTime(At.UtcDateTime));

        await AssertRoundTripsAsync(AnalyticsSamples.View(At));
        Assert.Equal(
            ["navigation"],
            await Database.QueryAsync("SELECT origin FROM analytics_event", Cancellation));
    }

    [Fact]
    public async Task AnalyticsDailyRoundTrips()
    {
        await AssertRoundTripsAsync(new AnalyticsDaily
        {
            Day = new DateOnly(2026, 9, 25),
            Source = AnalyticsDailySource.Import,
            Totals = """{"views": 3, "byHour": [1, 2], "botViews": 1, "byWeekday": [3]}""",
            // In jsonb's own order of keys, shorter first: what reading gives back.
            Buckets = """{"entities": {"item:3031": 1, "champion:Ahri": 2}}""",
            Visitors = """["4f1c", "9ab2"]""",
            CountryNames = """{"FR": "France"}""",
            UpdatedAt = At,
        });
        Assert.Equal(
            ["import"],
            await Database.QueryAsync("SELECT source FROM analytics_daily", Cancellation));
    }

    [Fact]
    public async Task ClientPolicyEntryRoundTrips()
    {
        await AssertRoundTripsAsync(new ClientPolicyEntry
        {
            Platform = AppPlatform.Android,
            MinimumVersion = "1.2.0",
            LatestVersion = "1.4.2",
            BundleId = "web-1.4.2-3f2a9c1",
            BundleUrl = "https://github.com/lodb/lodb/releases/download/v1.4.2/shell.zip",
            BundleChecksum = new string('a', 64),
            BundleSignature = "MEUCIQDk3pY6kRm0x9T8w3J1c2Vq+Z9Qx4vA==",
            BundleMinimumNativeVersion = "1.3.0",
            PublishedAt = At,
        });
        Assert.Equal(
            ["android"],
            await Database.QueryAsync("SELECT platform FROM client_policy", Cancellation));
    }

    private async Task PartitionsOf(DateOnly day) =>
        await new AnalyticsPartitions(Database.DataSource).CreateAsync(day, day, Cancellation);

    private async Task<int> SeedApiKeyAsync()
    {
        var owner = await SeedAsync(new User
        {
            Email = "owner@example.test",
            UserName = "owner",
            Roles = [],
            CreatedAt = At.AddTicks(-1_234_560),
        });
        var key = await SeedAsync(new ApiKey
        {
            Name = "Default",
            KeyHash = new string('b', 64),
            KeyPrefix = "lodb_bbbb",
            Plan = "credits",
            CreatedAt = At.AddTicks(-1_234_560),
            UserId = owner.Id,
        });
        return key.Id;
    }

    // In a context of its own, so that the entity read next comes from the database.
    private async Task<T> SeedAsync<T>(T entity)
        where T : class
    {
        await using var context = Database.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(Cancellation);
        return entity;
    }

    private async Task AssertRoundTripsAsync<T>(T expected)
        where T : class
    {
        await SeedAsync(expected);

        await using var context = Database.CreateContext();
        var actual = await context.Set<T>().AsNoTracking().SingleAsync(Cancellation);
        Assert.Equivalent(expected, actual, strict: true);
    }
}
