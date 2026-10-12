using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing.Lifecycle;

/// <summary>
/// A session completed while its payment is still on its way is granted once that payment
/// comes in (<c>checkout.session.async_payment_succeeded</c>), once whatever event repeats it,
/// and never when it fails (<c>checkout.session.async_payment_failed</c>).
/// </summary>
/// <remarks>
/// The tests share a host, each with accounts and Stripe identifiers of its own.
/// </remarks>
public sealed class AsyncPaymentTests : IClassFixture<SharedBillingApp>
{
    private const string SessionId = "cs_test_sepa";
    private const long Requests = 10_000;

    private readonly StripePayloads _stripe;

    public AsyncPaymentTests(SharedBillingApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        App.ResetDoubles();
        _stripe = host.Payloads();
    }

    private BillingApp App { get; }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SucceededPaymentCreditsThePackOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = _stripe.Pack(SessionId, buyer.Id, Requests);
        var succeeded = StripePayloads.Later(
            _stripe.Event("evt_paid", StripePayloads.AsyncPaymentSucceeded, Paid(session)),
            days: 3);

        await DeliverAsync(_stripe.Completed("evt_completed", Unpaid(session)));
        Assert.Equal(0, (await App.KeyAsync(key.Id)).CreditsBalance);
        await DeliverAsync(succeeded);
        await DeliverAsync(succeeded);
        await DeliverAsync(_stripe.Event(
            "evt_paid_again",
            StripePayloads.AsyncPaymentSucceeded,
            Paid(session)));

        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        var grant = Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(StripePayloads.CreatedAt.AddDays(3), grant.PurchasedAt);
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
        Assert.Equal(StripeCheckoutStatus.Paid, (await CheckoutAsync()).Status);
    }

    [Fact]
    public async Task SucceededPaymentMakesTheDonorASupporter()
    {
        var donor = await App.SeedUserAsync("Donatrice");
        var session = _stripe.Donation(SessionId, 2_500, donor.Id);

        await DeliverAsync(_stripe.Completed("evt_completed", Unpaid(session)));
        await DeliverAsync(_stripe.Event(
            "evt_paid",
            StripePayloads.AsyncPaymentSucceeded,
            Paid(session)));

        var delivered = _stripe.Id(SessionId);
        await using var db = App.Database();
        Assert.Equal(
            2_500,
            (await db.Donations.SingleAsync(
                donation => donation.StripeSessionId == delivered,
                Cancellation)).AmountCents);
        Assert.True((await db.Users.SingleAsync(user => user.Id == donor.Id, Cancellation))
            .IsSupporter);
    }

    [Fact]
    public async Task FailedPaymentGrantsNothing()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = Unpaid(_stripe.Pack(SessionId, buyer.Id, Requests));

        await DeliverAsync(_stripe.Completed("evt_completed", session));
        await DeliverAsync(_stripe.Event(
            "evt_failed",
            StripePayloads.AsyncPaymentFailed,
            session.DeepClone().AsObject()));

        Assert.Equal(0, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Empty(await App.GrantsOfAsync(key.Id));
        Assert.Empty(App.KeyCache.Invalidated);
        Assert.Equal(StripeCheckoutStatus.Failed, (await CheckoutAsync()).Status);
    }

    [Fact]
    public async Task SuccessDeliveredBeforeTheCompletionCreditsOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = _stripe.Pack(SessionId, buyer.Id, Requests);

        await DeliverAsync(_stripe.Event(
            "evt_paid",
            StripePayloads.AsyncPaymentSucceeded,
            Paid(session)));
        await DeliverAsync(_stripe.Completed("evt_completed", Unpaid(session)));

        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(StripeCheckoutStatus.Paid, (await CheckoutAsync()).Status);
    }

    // Stripe does not deliver in order: the refund or the dispute of the payment may come in
    // before its success.
    [Theory]
    [InlineData("refund")]
    [InlineData("dispute")]
    public async Task PaymentTakenBackBeforeItsSuccessGrantsNothing(string cause)
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = _stripe.Pack(SessionId, buyer.Id, Requests);
        var payment = _stripe.PaymentOf(SessionId);

        await DeliverAsync(_stripe.Completed("evt_completed", Unpaid(session)));
        await DeliverAsync(cause == "refund"
            ? _stripe.Refunded("evt_taken_back", payment)
            : _stripe.Disputed("evt_taken_back", payment));
        await DeliverAsync(_stripe.Event(
            "evt_paid",
            StripePayloads.AsyncPaymentSucceeded,
            Paid(session)));

        Assert.Equal(0, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Empty(await App.GrantsOfAsync(key.Id));
        Assert.Empty(App.KeyCache.Invalidated);
        Assert.Equal(StripeCheckoutStatus.Revoked, (await CheckoutAsync()).Status);
    }

    private static JsonObject Unpaid(JsonObject session)
    {
        var unpaid = session.DeepClone().AsObject();
        unpaid["payment_status"] = "unpaid";
        return unpaid;
    }

    private static JsonObject Paid(JsonObject session) => session.DeepClone().AsObject();

    private async Task DeliverAsync(JsonObject stripeEvent)
    {
        using var response = await App.DeliverAsync(stripeEvent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<StripeCheckout> CheckoutAsync()
    {
        var delivered = _stripe.Id(SessionId);
        await using var db = App.Database();
        return await db.StripeCheckouts.AsNoTracking()
            .SingleAsync(checkout => checkout.Id == delivered, Cancellation);
    }
}
