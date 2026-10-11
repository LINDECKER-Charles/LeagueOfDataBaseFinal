using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing.Lifecycle;

/// <summary>
/// A payment refunded in full (<c>charge.refunded</c>) or disputed
/// (<c>charge.dispute.created</c>) takes back what it granted: the requests of its pack not
/// spent yet, never below zero nor from another pack; a donation and the supporter badge it
/// gave; a plan. A partial refund, a repeated one or one the site cannot match takes nothing.
/// </summary>
public sealed class RevocationTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string FirstPack = "cs_test_pack_1";
    private const string SecondPack = "cs_test_pack_2";

    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RefundTakesBackWhatIsLeftOfThePack()
    {
        var (buyer, key) = await BuyerAsync();
        await BuyAsync(buyer, FirstPack, 5_000);
        await SpendDownToAsync(key, 3_000);

        await DeliverAsync(Later(StripePayloads.Refunded("evt_refund", Payment(FirstPack)), 1));

        var revoked = await App.KeyAsync(key.Id);
        Assert.Equal((0L, 10), (revoked.CreditsBalance, revoked.RateLimitPerMin));
        var grant = Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(
            ((long?)3_000, (DateTimeOffset?)StripePayloads.CreatedAt.AddDays(1)),
            (grant.ExpiredRequests, grant.ExpiredAt));
        Assert.Equal(StripeCheckoutStatus.Revoked, (await CheckoutAsync(FirstPack)).Status);
        Assert.Equal([key.Id, key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task RefundLeavesTheRequestsOfAnotherPack()
    {
        var (buyer, key) = await BuyerAsync();
        await BuyAsync(buyer, FirstPack, 5_000);
        await BuyAsync(buyer, SecondPack, 10_000);
        // Spent from the oldest pack first: 3 000 of the first one are left.
        await SpendDownToAsync(key, 13_000);

        await DeliverAsync(Later(StripePayloads.Refunded("evt_refund", Payment(FirstPack)), 2));

        var revoked = await App.KeyAsync(key.Id);
        Assert.Equal((10_000L, 60), (revoked.CreditsBalance, revoked.RateLimitPerMin));
        Assert.Equal(
            new (long, long?)[] { (5_000, 3_000), (10_000, null) },
            (await App.GrantsOfAsync(key.Id)).Select(static grant =>
                (grant.Requests, grant.ExpiredRequests)));
    }

    [Fact]
    public async Task RefundDeliveredTwiceTakesBackOnce()
    {
        var (buyer, key) = await BuyerAsync();
        await BuyAsync(buyer, FirstPack, 5_000);
        await BuyAsync(buyer, SecondPack, 10_000);

        await DeliverAsync(StripePayloads.Refunded("evt_refund_1", Payment(FirstPack)));
        await DeliverAsync(StripePayloads.Refunded("evt_refund_2", Payment(FirstPack)));

        Assert.Equal(10_000, (await App.KeyAsync(key.Id)).CreditsBalance);
    }

    [Fact]
    public async Task DisputeTakesThePackBack()
    {
        var (buyer, key) = await BuyerAsync();
        await BuyAsync(buyer, FirstPack, 20_000);

        await DeliverAsync(StripePayloads.Disputed("evt_dispute", Payment(FirstPack)));

        Assert.Equal((0L, 10), Rights(await App.KeyAsync(key.Id)));
        Assert.Equal(StripeCheckoutStatus.Revoked, (await CheckoutAsync(FirstPack)).Status);
    }

    [Fact]
    public async Task PartialRefundTakesNothingBack()
    {
        var (buyer, key) = await BuyerAsync();
        await BuyAsync(buyer, FirstPack, 5_000);
        var refund = StripePayloads.Refunded("evt_refund", Payment(FirstPack));
        var charge = refund["data"]!["object"]!.AsObject();
        charge["refunded"] = false;
        charge["amount_refunded"] = 300;

        await DeliverAsync(refund);

        Assert.Equal((5_000L, 60), Rights(await App.KeyAsync(key.Id)));
        Assert.Equal(StripeCheckoutStatus.Paid, (await CheckoutAsync(FirstPack)).Status);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RefundedDonationTakesItsBadgeBack(bool anotherGift, bool staysSupporter)
    {
        var (donor, _) = await BuyerAsync();
        await DeliverAsync(StripePayloads.Completed(
            "evt_gift_1",
            StripePayloads.Donation("cs_test_gift_1", 1_000, donor.Id)));
        if (anotherGift)
        {
            await DeliverAsync(StripePayloads.Completed(
                "evt_gift_2",
                StripePayloads.Donation("cs_test_gift_2", 500, donor.Id)));
        }

        await DeliverAsync(StripePayloads.Refunded("evt_refund", Payment("cs_test_gift_1")));

        await using var db = App.Database();
        var gifts = await db.Donations.AsNoTracking().ToListAsync(Cancellation);
        Assert.DoesNotContain(gifts, static gift => gift.StripeSessionId == "cs_test_gift_1");
        Assert.Equal(anotherGift ? 1 : 0, gifts.Count);
        Assert.Equal(staysSupporter, await IsSupporterAsync(db, donor));
    }

    [Fact]
    public async Task RefundedSubscriptionPaymentFallsBackToTheFreePlan()
    {
        var (buyer, key) = await BuyerAsync();
        await DeliverAsync(StripePayloads.Completed(
            "evt_subscribed",
            StripePayloads.Plan("cs_test_plan", buyer.Id, "monthly_plus")));

        await DeliverAsync(Later(
            StripePayloads.Refunded("evt_refund", "pi_test_invoice", StripePayloads.Customer),
            1));

        var revoked = await App.KeyAsync(key.Id);
        Assert.Equal(
            ("free", 500, StripePayloads.Subscription),
            (revoked.Plan, revoked.MonthlyQuota, revoked.StripeSubscriptionId));
    }

    [Fact]
    public async Task UnmatchedRefundIsAcknowledgedAndTakesNothing()
    {
        var (buyer, key) = await BuyerAsync();
        await BuyAsync(buyer, FirstPack, 5_000);

        await DeliverAsync(StripePayloads.Refunded("evt_refund", "pi_test_elsewhere"));

        Assert.Equal(5_000, (await App.KeyAsync(key.Id)).CreditsBalance);
        await using var db = App.Database();
        Assert.Equal(
            StripeEventStatus.Processed,
            (await db.StripeEvents.SingleAsync(e => e.Id == "evt_refund", Cancellation)).Status);
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static string Payment(string sessionId) => StripePayloads.PaymentOf(sessionId);

    private static JsonObject Later(JsonObject stripeEvent, int days) =>
        StripePayloads.Later(stripeEvent, days);

    private static (long Credits, int Rate) Rights(ApiKey key) =>
        (key.CreditsBalance, key.RateLimitPerMin);

    private static async Task<bool> IsSupporterAsync(LoDbDbContext db, User user) =>
        (await db.Users.AsNoTracking().SingleAsync(row => row.Id == user.Id, Cancellation))
            .IsSupporter;

    private async Task<(User Buyer, ApiKey Key)> BuyerAsync()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        return (buyer, await App.SeedKeyAsync(buyer));
    }

    // The second pack is paid a day after the first, which is then spent first.
    private Task BuyAsync(User buyer, string sessionId, long requests) =>
        DeliverAsync(Later(
            StripePayloads.Completed(
                "evt_" + sessionId,
                StripePayloads.Pack(sessionId, buyer.Id, requests)),
            sessionId == SecondPack ? 1 : 0));

    // What the public API leaves of the balance once requests are spent.
    private async Task SpendDownToAsync(ApiKey key, long balance)
    {
        await using var db = App.Database();
        await db.ApiKeys
            .Where(row => row.Id == key.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.CreditsBalance, balance),
                Cancellation);
    }

    private async Task DeliverAsync(JsonObject stripeEvent)
    {
        using var response = await App.DeliverAsync(stripeEvent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<StripeCheckout> CheckoutAsync(string sessionId)
    {
        await using var db = App.Database();
        return await db.StripeCheckouts.AsNoTracking()
            .SingleAsync(checkout => checkout.Id == sessionId, Cancellation);
    }
}
