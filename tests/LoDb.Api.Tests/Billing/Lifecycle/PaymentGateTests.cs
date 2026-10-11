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

    // "paid" is the status of every other session of these tests.
    [Fact]
    public async Task SessionWithoutPaymentToMakeIsGrantedAndPaid()
    {
        var session = StripePayloads.Donation("cs_test_don", 1_000);
        session["payment_status"] = "no_payment_required";

        await DeliverAsync(session);

        await using var db = App.Database();
        Assert.Single(await db.Donations.ToListAsync(Cancellation));
        Assert.Equal(StripeCheckoutStatus.Paid, (await CheckoutAsync("cs_test_don")).Status);
    }

    // The prices each kind must charge are CataloguePricesTests'.
    [Fact]
    public async Task OffCatalogueSessionIsRefused()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = With(StripePayloads.Pack("cs_test_off", 0, 5_000), "amount_total", 499);
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
