using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.PublicApiKeys.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.PublicApiKeys;

/// <summary>
/// A regenerated key changes its secret only: plan, quota, rate, credits with their grants,
/// Stripe identifiers and the month's usage move to the new key, the old one is revoked, and
/// <c>/v1</c> knows it from the next request.
/// </summary>
public sealed class RegenerationTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private KeysApp? _app;

    private KeysApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RegenerationKeepsEverythingButTheSecret()
    {
        var owner = await App.SeedUserAsync("Abonne");
        using var browser = await App.SignedInAsync(owner);
        var first = await IssueAsync(browser);
        var previous = await PaidAsync(owner.Id);

        using var response = await browser.PostAsync(KeysApp.RegeneratePath);

        var issued = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        var secret = ApiJson.Text(issued, "secret")!;
        Assert.NotEqual(first, secret);
        var keys = await App.KeysOfAsync(owner.Id);
        Assert.Equal(2, keys.Count);
        var (old, current) = (keys[0], keys[1]);
        Assert.Equal((false, 0L), (old.IsActive, old.CreditsBalance));
        Assert.NotNull(old.RevokedAt);
        Assert.True(current.IsActive);
        Assert.Equal(
            (previous.Name, previous.Plan, previous.MonthlyQuota, previous.RateLimitPerMin),
            (current.Name, current.Plan, current.MonthlyQuota, current.RateLimitPerMin));
        Assert.Equal(
            (previous.CreditsBalance, previous.StripeCustomerId, previous.StripeSubscriptionId),
            (current.CreditsBalance, current.StripeCustomerId, current.StripeSubscriptionId));
        var key = issued.GetProperty("key");
        Assert.Equal(120, key.GetProperty("usedThisMonth").GetInt64());
        Assert.Equal(14_880, key.GetProperty("remainingThisMonth").GetInt64());
        Assert.True(key.GetProperty("subscribed").GetBoolean());
    }

    [Fact]
    public async Task UsageAndGrantsFollowTheNewKey()
    {
        var owner = await App.SeedUserAsync("Lignee");
        using var browser = await App.SignedInAsync(owner);
        await IssueAsync(browser);
        var previous = await PaidAsync(owner.Id);

        using var response = await browser.PostAsync(KeysApp.RegeneratePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = (await App.KeysOfAsync(owner.Id))[^1];
        await using var db = App.Database();
        Assert.Equal(
            [current.Id, current.Id],
            await db.ApiUsage.Select(static usage => usage.ApiKeyId).ToListAsync(Cancellation));
        Assert.Equal(
            current.Id,
            (await db.ApiCreditGrants.SingleAsync(Cancellation)).ApiKeyId);
        Assert.Equal([previous.Id, current.Id], App.KeyCache.Invalidated.Skip(1));
        var entry = Assert.Single(
            await App.AuditAsync(),
            static entry => entry.Action == AuditAction.ApiKeyRegenerate);
        Assert.Equal(current.KeyPrefix + "…", entry.Target);
    }

    [Fact]
    public async Task TheOldSecretIsRefusedOnV1AtOnce()
    {
        var owner = await App.SeedUserAsync("Rotation");
        using var browser = await App.SignedInAsync(owner);
        var old = await IssueAsync(browser);
        using var before = await App.AskV1Async(old);

        using var response = await browser.PostAsync(KeysApp.RegeneratePath);
        var secret = ApiJson.Text(await ApiJson.ReadAsync(response, HttpStatusCode.OK), "secret")!;
        using var withOld = await App.AskV1Async(old);
        using var withNew = await App.AskV1Async(secret);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withOld.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
    }

    [Fact]
    public async Task AnUnverifiedAccountRegeneratesTheKeyAPaymentIssued()
    {
        var owner = await App.SeedUserAsync("Acheteur", verified: false);
        await using (var db = App.Database())
        {
            db.ApiKeys.Add(Key(owner, "lodb_0123456"));
            await db.SaveChangesAsync(Cancellation);
        }

        using var browser = await App.SignedInAsync(owner);
        using var response = await browser.PostAsync(KeysApp.RegeneratePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([false, true], (await App.KeysOfAsync(owner.Id)).Select(k => k.IsActive));
    }

    [Fact]
    public async Task NothingToRegenerateWithoutAKey()
    {
        using var browser = await App.SignedInAsync(await App.SeedUserAsync("SansCle"));

        using var response = await browser.PostAsync(KeysApp.RegeneratePath);

        Assert.Equal(
            "api-key-missing",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.NotFound));
    }

    public async ValueTask InitializeAsync() => _app = await KeysApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static async Task<string> IssueAsync(BrowserClient browser)
    {
        using var response = await browser.PostAsync(KeysApp.PortalPath, new { name = "prod" });
        return ApiJson.Text(await ApiJson.ReadAsync(response, HttpStatusCode.OK), "secret")!;
    }

    private static ApiKey Key(User owner, string prefix) => new()
    {
        Name = "default",
        KeyHash = new string('a', 64),
        KeyPrefix = prefix,
        Plan = "free",
        MonthlyQuota = 500,
        RateLimitPerMin = 60,
        CreditsBalance = 5_000,
        IsActive = true,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UserId = owner.Id,
    };

    // Turns the account's key into a subscribed one holding credits, metered this month and
    // last month, as payments and /v1 would have.
    private async Task<ApiKey> PaidAsync(int userId)
    {
        await using var db = App.Database();
        var key = await db.ApiKeys.SingleAsync(k => k.UserId == userId, Cancellation);
        key.Plan = "monthly";
        key.MonthlyQuota = 15_000;
        key.RateLimitPerMin = 120;
        key.CreditsBalance = 4_321;
        key.StripeCustomerId = "cus_test_portal";
        key.StripeSubscriptionId = "sub_test_portal";
        var today = DateOnly.FromDateTime(App.Clock.GetUtcNow().UtcDateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        db.ApiUsage.AddRange(
            new ApiUsage { ApiKeyId = key.Id, Day = today, Requests = 120 },
            new ApiUsage { ApiKeyId = key.Id, Day = month.AddDays(-1), Requests = 7 });
        var bought = App.Clock.GetUtcNow().AddDays(-3);
        db.ApiCreditGrants.Add(new ApiCreditGrant
        {
            ApiKeyId = key.Id,
            Source = ApiCreditGrantSource.Purchase,
            Requests = 5_000,
            PurchasedAt = bought,
            ExpiresAt = bought.AddMonths(ApiCreditGrant.ValidityMonths),
            StripeSessionId = "cs_test_portal",
        });
        await db.SaveChangesAsync(Cancellation);
        return key;
    }
}
