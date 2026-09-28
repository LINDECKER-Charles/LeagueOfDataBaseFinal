using System.Net;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// <c>customer.subscription.deleted</c>: the active key of the subscription goes back to the
/// free plan, at the credits rate while it holds credits, and keeps its Stripe customer.
/// </summary>
public sealed class SubscriptionDeletedTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

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
        Assert.Equal(StripePayloads.Customer, released.StripeCustomerId);
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task RevokedKeyKeepsItsPlan()
    {
        var owner = await App.SeedUserAsync("Abonne");
        var key = await SubscribedKeyAsync(owner, credits: 0, active: false);

        await CancelAsync();

        var revoked = await App.KeyAsync(key.Id);
        Assert.Equal(("annual", StripePayloads.Subscription),
            (revoked.Plan, revoked.StripeSubscriptionId));
        Assert.Empty(App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task SubscriptionMatchingNoKeyIsAcknowledged()
    {
        await CancelAsync();

        await using var db = App.Database();
        Assert.Equal(
            StripeEventStatus.Processed,
            (await db.StripeEvents.SingleAsync(Cancellation)).Status);
        Assert.Empty(App.KeyCache.Invalidated);
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private Task<ApiKey> SubscribedKeyAsync(User owner, long credits, bool active = true) =>
        App.SeedKeyAsync(owner, key =>
        {
            key.Plan = "annual";
            key.MonthlyQuota = 20_000;
            key.RateLimitPerMin = 300;
            key.CreditsBalance = credits;
            key.IsActive = active;
            key.StripeCustomerId = StripePayloads.Customer;
            key.StripeSubscriptionId = StripePayloads.Subscription;
        });

    private async Task CancelAsync()
    {
        using var response = await App.DeliverAsync(StripePayloads.Cancelled("evt_cancel"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
