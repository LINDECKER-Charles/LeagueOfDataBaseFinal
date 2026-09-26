using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.PublicApiKeys.Support;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.PublicApiKeys;

/// <summary>
/// What the portal shows of a key: its rights, the month's consumption against its quota,
/// and the metered days of the last 30, newest first; and the reference of
/// <c>/developers</c>, public.
/// </summary>
public sealed class OverviewTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string ReferencePath = "/api/public-api/reference";

    private KeysApp? _app;

    private KeysApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OverviewCountsTheMonthAndListsThirtyDays()
    {
        var owner = await App.SeedUserAsync("Compteur");
        using var browser = await App.SignedInAsync(owner);
        using var created = await browser.PostAsync(KeysApp.PortalPath, new { name = "stats" });
        var today = DateOnly.FromDateTime(App.Clock.GetUtcNow().UtcDateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        await MeterAsync(owner.Id, (today, 40), (month, 2), (today.AddDays(-30), 5),
            (today.AddDays(-31), 900));

        using var response = await browser.GetAsync(KeysApp.PortalPath);

        var key = (await ApiJson.ReadAsync(response, HttpStatusCode.OK)).GetProperty("key");
        var monthUse = today == month ? 40 : 42;
        Assert.Equal(monthUse, key.GetProperty("usedThisMonth").GetInt64());
        Assert.Equal(500 - monthUse, key.GetProperty("remainingThisMonth").GetInt64());
        var days = key.GetProperty("usage").EnumerateArray()
            .Select(static day => DateOnly.Parse(ApiJson.Text(day, "day")!, CultureInfo.InvariantCulture))
            .ToList();
        Assert.Equal(days.OrderByDescending(static day => day), days);
        Assert.Equal(today.AddDays(-30), days[^1]);
        Assert.DoesNotContain(today.AddDays(-31), days);
        Assert.Equal(
            ("free", 10, 0L, false),
            (ApiJson.Text(key, "plan"), key.GetProperty("rateLimitPerMin").GetInt32(),
                key.GetProperty("creditsBalance").GetInt64(),
                key.GetProperty("subscribed").GetBoolean()));
    }

    [Fact]
    public async Task ReferenceStatesTheConfiguredOriginAndTheOffers()
    {
        using var browser = App.Browser();

        using var response = await browser.GetAsync(ReferencePath);

        var reference = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal("https://api.example.test", ApiJson.Text(reference, "baseUrl"));
        Assert.Equal("lodb_", ApiJson.Text(reference, "keyPrefix"));
        var free = reference.GetProperty("freePlan");
        Assert.Equal(
            (500, 10, 60),
            (free.GetProperty("monthlyQuota").GetInt32(),
                free.GetProperty("ratePerMinute").GetInt32(),
                reference.GetProperty("creditsRatePerMinute").GetInt32()));
        Assert.Equal(["small", "medium", "large"], Codes(reference, "packs"));
        Assert.Equal(["monthly", "monthly_plus", "annual", "annual_plus"], Codes(reference, "plans"));
    }

    public async ValueTask InitializeAsync() => _app = await KeysApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static IEnumerable<string?> Codes(JsonElement reference, string offers) =>
        reference.GetProperty(offers).EnumerateArray()
            .Select(static offer => ApiJson.Text(offer, "code"));

    private async Task MeterAsync(int userId, params (DateOnly Day, long Requests)[] days)
    {
        await using var db = App.Database();
        var keyId = await db.ApiKeys
            .Where(key => key.UserId == userId)
            .Select(static key => key.Id)
            .SingleAsync(Cancellation);
        db.ApiUsage.AddRange(days.DistinctBy(static day => day.Day).Select(day => new ApiUsage
        {
            ApiKeyId = keyId,
            Day = day.Day,
            Requests = day.Requests,
        }));
        await db.SaveChangesAsync(Cancellation);
    }
}
