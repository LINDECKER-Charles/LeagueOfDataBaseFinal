using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// An event is applied once: a redelivery, even at the same time, is acknowledged without
/// crediting again, and a handler that fails leaves nothing behind, so that the next
/// delivery applies it whole.
/// </summary>
public sealed class WebhookIdempotencyTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const long Requests = 5_000;

    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EventDeliveredTwiceCreditsOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var paid = StripePayloads.Completed(
            "evt_pack",
            StripePayloads.Pack("cs_test_pack", buyer.Id, Requests));

        using var first = await App.DeliverAsync(paid);
        using var second = await App.DeliverAsync(paid);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Single(await EventsAsync());
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task SessionPaidUnderTwoEventsCreditsOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var session = StripePayloads.Pack("cs_test_pack", buyer.Id, Requests);

        using var first = await App.DeliverAsync(StripePayloads.Completed("evt_1", session));
        using var second = await App.DeliverAsync(
            StripePayloads.Completed("evt_2", session.DeepClone().AsObject()));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(2, (await EventsAsync()).Count);
    }

    [Fact]
    public async Task DeliveriesAtTheSameTimeCreditOnce()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var paid = StripePayloads.Completed(
            "evt_pack",
            StripePayloads.Pack("cs_test_pack", buyer.Id, Requests));

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

    [Fact]
    public async Task FailingHandlerWritesNothingAndTheRedeliveryApplies()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var paid = StripePayloads.Completed(
            "evt_pack",
            StripePayloads.Pack("cs_test_pack", buyer.Id, Requests));
        App.Fault.Arm();

        using var failed = await App.DeliverAsync(paid);

        Assert.Equal(
            "handler-failed",
            await ApiJson.ProblemCodeAsync(failed, HttpStatusCode.InternalServerError));
        Assert.Equal(0, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Empty(await App.GrantsOfAsync(key.Id));
        Assert.Empty(await EventsAsync());
        Assert.Empty(App.KeyCache.Invalidated);

        using var redelivered = await App.DeliverAsync(paid);

        Assert.Equal(HttpStatusCode.OK, redelivered.StatusCode);
        Assert.Equal(Requests, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(StripeEventStatus.Processed, Assert.Single(await EventsAsync()).Status);
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task FailingHandlerTakesBackTheKeyItIssued()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var paid = StripePayloads.Completed(
            "evt_pack",
            StripePayloads.Pack("cs_test_pack", buyer.Id, Requests));
        App.Fault.Arm();

        using var failed = await App.DeliverAsync(paid);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Empty(await App.KeysOfAsync(buyer.Id));
        Assert.Empty(await App.AuditAsync());

        using var redelivered = await App.DeliverAsync(paid);

        Assert.Equal(HttpStatusCode.OK, redelivered.StatusCode);
        var key = Assert.Single(await App.KeysOfAsync(buyer.Id));
        Assert.Equal(Requests, key.CreditsBalance);
        Assert.Equal(
            [AuditAction.ApiKeyCreate],
            (await App.AuditAsync()).Select(static entry => entry.Action));
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private async Task<List<StripeEvent>> EventsAsync()
    {
        await using var db = App.Database();
        return await db.StripeEvents.AsNoTracking().ToListAsync(Cancellation);
    }
}
