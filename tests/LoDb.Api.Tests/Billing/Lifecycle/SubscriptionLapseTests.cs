using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing.Lifecycle;

/// <summary>
/// The key of a subscription follows what Stripe reports of it: the free plan while it is past
/// due or unpaid, or once a renewal cannot be charged; its plan back once it is active again;
/// an event older than the last one applied changes nothing; an ended subscription stays gone.
/// </summary>
/// <remarks>
/// The tests share a host, each with an account and a subscription of its own.
/// </remarks>
public sealed class SubscriptionLapseTests : IClassFixture<SharedBillingApp>
{
    private readonly StripePayloads _stripe;

    public SubscriptionLapseTests(SharedBillingApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        App.ResetDoubles();
        _stripe = host.Payloads();
    }

    private BillingApp App { get; }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // "unpaid", the other lapse, falls back as well in OlderEventChangesNothing.
    [Fact]
    public async Task LapsedSubscriptionFallsBackToTheFreePlan()
    {
        var key = await SubscribedKeyAsync();

        await DeliverAsync(
            StripePayloads.Later(_stripe.Updated("evt_lapse", "past_due"), 1));

        await AssertFreeAsync(key);
        Assert.Equal(
            StripeSubscriptionStatus.Suspended,
            (await SubscriptionAsync()).Status);
    }

    [Fact]
    public async Task FailedRenewalFallsBackToTheFreePlan()
    {
        var key = await SubscribedKeyAsync();

        await DeliverAsync(StripePayloads.Later(_stripe.InvoiceFailed("evt_invoice"), 1));

        await AssertFreeAsync(key);
    }

    // An endpoint pinned to an API version older than 2025-03-31 names the subscription at the
    // top of the invoice.
    [Fact]
    public async Task FailedRenewalOfAnOlderApiVersionFallsBackToTheFreePlan()
    {
        var key = await SubscribedKeyAsync();
        var failed = StripePayloads.Later(_stripe.InvoiceFailed("evt_invoice"), 1);
        var invoice = failed["data"]!["object"]!.AsObject();
        invoice.Remove("parent");
        invoice["subscription"] = _stripe.Subscription;

        await DeliverAsync(failed);

        await AssertFreeAsync(key);
    }

    [Fact]
    public async Task ActiveAgainRestoresThePlan()
    {
        var key = await SubscribedKeyAsync();

        await DeliverAsync(StripePayloads.Later(_stripe.InvoiceFailed("evt_invoice"), 1));
        await DeliverAsync(StripePayloads.Later(
            _stripe.Updated("evt_recovered", "active"),
            2));

        await AssertAnnualAsync(key);
        Assert.Equal([key.Id, key.Id, key.Id], App.KeyCache.Invalidated);
        Assert.Equal(
            (StripeSubscriptionStatus.Entitled, StripePayloads.CreatedAt.AddDays(2)),
            ((await SubscriptionAsync()).Status, (await SubscriptionAsync()).ChangedAt));
    }

    [Fact]
    public async Task OlderEventChangesNothing()
    {
        var key = await SubscribedKeyAsync();

        await DeliverAsync(StripePayloads.Later(_stripe.Updated("evt_lapse", "unpaid"), 2));
        await DeliverAsync(StripePayloads.Later(_stripe.Updated("evt_old", "active"), 1));

        await AssertFreeAsync(key);
    }

    [Fact]
    public async Task SubscriptionSoldBeforeTheLedgerIsFollowedToo()
    {
        var owner = await App.SeedUserAsync("Ancien");
        var key = await App.SeedKeyAsync(owner, key =>
        {
            key.Plan = "annual";
            key.MonthlyQuota = 20_000;
            key.RateLimitPerMin = 300;
            key.CreditsBalance = 3_000;
            key.StripeCustomerId = _stripe.Customer;
            key.StripeSubscriptionId = _stripe.Subscription;
        });

        await DeliverAsync(_stripe.Updated("evt_lapse", "past_due"));

        var lapsed = await App.KeyAsync(key.Id);
        Assert.Equal(("free", 500, 60), (lapsed.Plan, lapsed.MonthlyQuota, lapsed.RateLimitPerMin));
        await DeliverAsync(StripePayloads.Later(_stripe.Updated("evt_paid", "active"), 1));
        await AssertAnnualAsync(key);
    }

    [Fact]
    public async Task EndedSubscriptionIsNotRestored()
    {
        var key = await SubscribedKeyAsync();

        await DeliverAsync(StripePayloads.Later(_stripe.Cancelled("evt_end"), 1));
        await DeliverAsync(StripePayloads.Later(_stripe.Updated("evt_late", "active"), 2));

        var ended = await App.KeyAsync(key.Id);
        Assert.Equal(("free", (string?)null), (ended.Plan, ended.StripeSubscriptionId));
        Assert.Equal(StripeSubscriptionStatus.Ended, (await SubscriptionAsync()).Status);
    }

    [Fact]
    public async Task IncompleteSubscriptionChangesNothing()
    {
        var key = await SubscribedKeyAsync();

        await DeliverAsync(StripePayloads.Later(_stripe.Updated("evt", "incomplete"), 1));

        await AssertAnnualAsync(key);
    }

    // Subscribed through a paid checkout, as the site sells it.
    private async Task<ApiKey> SubscribedKeyAsync()
    {
        var buyer = await App.SeedUserAsync("Abonne");
        var key = await App.SeedKeyAsync(buyer);
        await DeliverAsync(_stripe.Completed(
            "evt_subscribed",
            _stripe.Plan("cs_test_plan", buyer.Id, "annual")));
        await AssertAnnualAsync(key);
        return key;
    }

    private async Task AssertAnnualAsync(ApiKey key)
    {
        var subscribed = await App.KeyAsync(key.Id);
        Assert.Equal(
            ("annual", 20_000, 300, _stripe.Subscription),
            (subscribed.Plan, subscribed.MonthlyQuota, subscribed.RateLimitPerMin,
                subscribed.StripeSubscriptionId));
    }

    // The subscription stays on the key, so that it can come back.
    private async Task AssertFreeAsync(ApiKey key)
    {
        var lapsed = await App.KeyAsync(key.Id);
        Assert.Equal(
            ("free", 500, 10, _stripe.Subscription),
            (lapsed.Plan, lapsed.MonthlyQuota, lapsed.RateLimitPerMin,
                lapsed.StripeSubscriptionId));
    }

    private async Task DeliverAsync(JsonObject stripeEvent)
    {
        using var response = await App.DeliverAsync(stripeEvent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<StripeSubscription> SubscriptionAsync()
    {
        var subscription = _stripe.Subscription;
        await using var db = App.Database();
        return await db.StripeSubscriptions.AsNoTracking()
            .SingleAsync(row => row.Id == subscription, Cancellation);
    }
}
