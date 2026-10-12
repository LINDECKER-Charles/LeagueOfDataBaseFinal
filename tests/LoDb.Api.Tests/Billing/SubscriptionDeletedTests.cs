using System.Net;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// <c>customer.subscription.deleted</c>: the active key of the subscription goes back to the
/// free plan, at the credits rate while it holds credits, and keeps its Stripe customer.
/// </summary>
/// <remarks>
/// The tests share a host, each with an account and a subscription of its own.
/// </remarks>
public sealed class SubscriptionDeletedTests : IClassFixture<SharedBillingApp>
{
    private readonly StripePayloads _stripe;

    public SubscriptionDeletedTests(SharedBillingApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        App.ResetDoubles();
        _stripe = host.Payloads();
    }

    private BillingApp App { get; }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(0, 10)]
    [InlineData(3_000, 60)]
    public async Task CancellationFallsBackToTheFreePlan(long credits, int rate)
    {
        var owner = await App.SeedUserAsync("Abonne");
        var key = await SubscribedKeyAsync(owner, credits);

        await CancelAsync();

        var released = await App.KeyAsync(key.Id);
        Assert.Equal(
            ("free", 500, rate, credits),
            (released.Plan, released.MonthlyQuota, released.RateLimitPerMin,
                released.CreditsBalance));
        Assert.Null(released.StripeSubscriptionId);
        Assert.Equal(_stripe.Customer, released.StripeCustomerId);
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task RevokedKeyKeepsItsPlan()
    {
        var owner = await App.SeedUserAsync("Abonne");
        var key = await SubscribedKeyAsync(owner, credits: 0, active: false);

        await CancelAsync();

        var revoked = await App.KeyAsync(key.Id);
        Assert.Equal(("annual", _stripe.Subscription),
            (revoked.Plan, revoked.StripeSubscriptionId));
        Assert.Empty(App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task SubscriptionMatchingNoKeyIsAcknowledged()
    {
        await CancelAsync();

        var delivered = _stripe.Id("evt_cancel");
        await using var db = App.Database();
        Assert.Equal(
            StripeEventStatus.Processed,
            (await db.StripeEvents.SingleAsync(
                stripeEvent => stripeEvent.Id == delivered,
                Cancellation)).Status);
        Assert.Empty(App.KeyCache.Invalidated);
    }

    private Task<ApiKey> SubscribedKeyAsync(User owner, long credits, bool active = true) =>
        App.SeedKeyAsync(owner, key =>
        {
            key.Plan = "annual";
            key.MonthlyQuota = 20_000;
            key.RateLimitPerMin = 300;
            key.CreditsBalance = credits;
            key.IsActive = active;
            key.StripeCustomerId = _stripe.Customer;
            key.StripeSubscriptionId = _stripe.Subscription;
        });

    private async Task CancelAsync()
    {
        using var response = await App.DeliverAsync(_stripe.Cancelled("evt_cancel"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
