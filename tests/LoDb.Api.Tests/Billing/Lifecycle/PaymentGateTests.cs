using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing.Lifecycle;

/// <summary>
/// <c>checkout.session.completed</c> grants a session only once its money is in, and only at
/// the catalogue's price: an unpaid session is recorded as pending, a session charging
/// another amount or currency is refused, and neither grants anything.
/// </summary>
public sealed class PaymentGateTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnpaidPackGrantsNothingAndIsPending()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);

        await DeliverAsync(Unpaid(StripePayloads.Pack("cs_test_sepa", buyer.Id, 5_000)));

        var untouched = await App.KeyAsync(key.Id);
        Assert.Equal((0L, 10), (untouched.CreditsBalance, untouched.RateLimitPerMin));
        Assert.Empty(await App.GrantsOfAsync(key.Id));
        Assert.Empty(App.KeyCache.Invalidated);
        var checkout = await CheckoutAsync("cs_test_sepa");
        Assert.Equal(
            (StripeCheckoutKind.Pack, StripeCheckoutStatus.Pending, 500L, "eur"),
            (checkout.Kind, checkout.Status, checkout.AmountCents, checkout.Currency));
        Assert.Equal(StripePayloads.PaymentOf("cs_test_sepa"), checkout.PaymentIntentId);
    }

    [Fact]
    public async Task UnpaidPlanLeavesTheKeyOnItsPlan()
    {
        var buyer = await App.SeedUserAsync("Abonne");
        var key = await App.SeedKeyAsync(buyer);

        await DeliverAsync(Unpaid(StripePayloads.Plan("cs_test_plan", buyer.Id, "annual")));

        var untouched = await App.KeyAsync(key.Id);
        Assert.Equal(("free", (string?)null), (untouched.Plan, untouched.StripeSubscriptionId));
        await using var db = App.Database();
        Assert.Empty(await db.StripeSubscriptions.ToListAsync(Cancellation));
    }

    [Fact]
    public async Task UnpaidDonationMakesNoSupporter()
    {
        var donor = await App.SeedUserAsync("Donatrice");

        await DeliverAsync(Unpaid(StripePayloads.Donation("cs_test_don", 1_000, donor.Id)));

        await using var db = App.Database();
        Assert.Empty(await db.Donations.ToListAsync(Cancellation));
        Assert.False((await db.Users.SingleAsync(user => user.Id == donor.Id, Cancellation))
            .IsSupporter);
        Assert.Equal(StripeCheckoutStatus.Pending, (await CheckoutAsync("cs_test_don")).Status);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("no_payment_required")]
    public async Task SettledSessionIsGrantedAndPaid(string paymentStatus)
    {
        var session = StripePayloads.Donation("cs_test_don", 1_000);
        session["payment_status"] = paymentStatus;

        await DeliverAsync(session);

        await using var db = App.Database();
        Assert.Single(await db.Donations.ToListAsync(Cancellation));
        Assert.Equal(StripeCheckoutStatus.Paid, (await CheckoutAsync("cs_test_don")).Status);
    }

    [Theory]
    [InlineData("pack below its price")]
    [InlineData("pack in dollars")]
    [InlineData("pack of requests not on sale")]
    [InlineData("pack without amount")]
    [InlineData("plan below its price")]
    [InlineData("plan not on sale")]
    [InlineData("donation above the tiers")]
    [InlineData("donation below the tiers")]
    public async Task OffCatalogueSessionIsRefused(string offer)
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = OffCatalogue(offer);
        session["metadata"]!["user_id"] = buyer.Id.ToString(CultureInfo.InvariantCulture);
        session["client_reference_id"] = buyer.Id.ToString(CultureInfo.InvariantCulture);

        await DeliverAsync(session);

        var untouched = await App.KeyAsync(key.Id);
        Assert.Equal(("free", 0L), (untouched.Plan, untouched.CreditsBalance));
        Assert.Empty(await App.GrantsOfAsync(key.Id));
        await using var db = App.Database();
        Assert.Empty(await db.Donations.ToListAsync(Cancellation));
        Assert.False((await db.Users.SingleAsync(user => user.Id == buyer.Id, Cancellation))
            .IsSupporter);
        Assert.Equal(StripeCheckoutStatus.Refused, (await CheckoutAsync("cs_test_off")).Status);
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static JsonObject OffCatalogue(string offer) => offer switch
    {
        "pack below its price" => With(Pack(), "amount_total", 499),
        "pack in dollars" => With(Pack(), "currency", "usd"),
        "pack of requests not on sale" => Pack(requests: 7_000),
        "pack without amount" => With(Pack(), "amount_total", null),
        "plan below its price" => With(Plan(), "amount_total", 100),
        "plan not on sale" => Plan("lifetime"),
        "donation above the tiers" => Donation(50_001),
        "donation below the tiers" => Donation(99),
        _ => throw new ArgumentOutOfRangeException(nameof(offer), offer, null),
    };

    private static JsonObject Pack(long requests = 5_000) =>
        StripePayloads.Pack("cs_test_off", 0, requests);

    private static JsonObject Plan(string plan = "annual") =>
        StripePayloads.Plan("cs_test_off", 0, plan);

    private static JsonObject Donation(int amountCents) =>
        StripePayloads.Donation("cs_test_off", amountCents);

    private static JsonObject With(JsonObject session, string field, JsonNode? value)
    {
        session[field] = value;
        return session;
    }

    private static JsonObject Unpaid(JsonObject session) =>
        With(session, "payment_status", "unpaid");

    private async Task DeliverAsync(JsonObject session)
    {
        using var response = await App.DeliverAsync(
            StripePayloads.Completed("evt_completed", session));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<StripeCheckout> CheckoutAsync(string sessionId)
    {
        await using var db = App.Database();
        return await db.StripeCheckouts.AsNoTracking()
            .SingleAsync(checkout => checkout.Id == sessionId, Cancellation);
    }
}
