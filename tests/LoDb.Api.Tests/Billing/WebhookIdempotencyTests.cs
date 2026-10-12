using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Billing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// An event is applied once: a redelivery, even at the same time, is acknowledged without
/// crediting again, and a handler that fails leaves nothing behind, so that the next
/// delivery applies it whole.
/// </summary>
/// <remarks>
/// The tests share a host, each with accounts and Stripe identifiers of its own.
/// </remarks>
public sealed class WebhookIdempotencyTests : IClassFixture<SharedBillingApp>
{
    private const long Requests = 5_000;

    private readonly StripePayloads _stripe;

    public WebhookIdempotencyTests(SharedBillingApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        App.ResetDoubles();
        _stripe = host.Payloads();
    }

    private BillingApp App { get; }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EventDeliveredTwiceCreditsOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var paid = _stripe.Completed(
            "evt_pack",
            _stripe.Pack("cs_test_pack", buyer.Id, Requests));

        using var first = await App.DeliverAsync(paid);
        using var second = await App.DeliverAsync(paid);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Single(await EventsAsync("evt_pack"));
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task SessionPaidUnderTwoEventsCreditsOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = _stripe.Pack("cs_test_pack", buyer.Id, Requests);

        using var first = await App.DeliverAsync(_stripe.Completed("evt_1", session));
        using var second = await App.DeliverAsync(
            _stripe.Completed("evt_2", session.DeepClone().AsObject()));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(2, (await EventsAsync("evt_1", "evt_2")).Count);
    }

    [Fact]
    public async Task DeliveriesAtTheSameTimeCreditOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var paid = _stripe.Completed(
            "evt_pack",
            _stripe.Pack("cs_test_pack", buyer.Id, Requests));

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => App.DeliverAsync(paid)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
    }

    // A buyer without a key: the failed delivery takes back the key it issued, its journal
    // entry and the notice to /v1, as well as the credits.
    [Fact]
    public async Task FailingHandlerWritesNothingAndTheRedeliveryApplies()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var paid = _stripe.Completed(
            "evt_pack",
            _stripe.Pack("cs_test_pack", buyer.Id, Requests));
        App.Fault.Arm();

        using var failed = await App.DeliverAsync(paid);

        Assert.Equal(
            "handler-failed",
            await ApiJson.ProblemCodeAsync(failed, HttpStatusCode.InternalServerError));
        Assert.Empty(await App.KeysOfAsync(buyer.Id));
        Assert.Empty(await App.AuditOfAsync(buyer.Id));
        Assert.Empty(await EventsAsync("evt_pack"));
        Assert.Empty(App.KeyCache.Invalidated);

        using var redelivered = await App.DeliverAsync(paid);

        Assert.Equal(HttpStatusCode.OK, redelivered.StatusCode);
        var key = Assert.Single(await App.KeysOfAsync(buyer.Id));
        Assert.Equal(Requests, key.CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(
            StripeEventStatus.Processed,
            Assert.Single(await EventsAsync("evt_pack")).Status);
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
        Assert.Equal(
            [AuditAction.ApiKeyCreate],
            (await App.AuditOfAsync(buyer.Id)).Select(static entry => entry.Action));
    }

    // The events the test names, as delivered.
    private async Task<List<StripeEvent>> EventsAsync(params string[] ids)
    {
        var delivered = ids.Select(_stripe.Id).ToList();
        await using var db = App.Database();
        return await db.StripeEvents.AsNoTracking()
            .Where(stripeEvent => delivered.Contains(stripeEvent.Id))
            .ToListAsync(Cancellation);
    }
}
