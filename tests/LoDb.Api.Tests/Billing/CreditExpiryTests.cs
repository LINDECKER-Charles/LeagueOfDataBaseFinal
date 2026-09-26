using System.Net;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Api.Workers;
using LoDb.Api.Workers.Billing;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// The daily expiry of the credits, on a simulated clock: requests are spent from the oldest
/// grants, so a grant twelve months old takes off only what is left of it; a balance no grant
/// covers is dated first, from the day it is found.
/// </summary>
public sealed class CreditExpiryTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private static readonly TimeSpan Precision = TimeSpan.FromMilliseconds(1);

    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void ExpiryJobIsDiscoveredAsAWorker()
    {
        Assert.Contains(
            typeof(CreditExpiryJob),
            ConventionalWorkers.Discover(typeof(Program).Assembly));
    }

    [Fact]
    public async Task DueGrantTakesOffWhatIsLeftOfIt()
    {
        var now = App.Clock.GetUtcNow();
        var key = await KeyWithBalanceAsync(7_000);
        var old = Grant(key, 5_000, now.AddMonths(-13));
        var recent = Grant(key, 5_000, now.AddMonths(-1));
        await App.SeedGrantsAsync(old, recent);

        Assert.Equal(JobRunOutcome.Succeeded, await RunAsync());

        Assert.Equal(5_000, (await App.KeyAsync(key.Id)).CreditsBalance);
        var grants = await App.GrantsOfAsync(key.Id);
        Assert.Equal(2_000, grants[0].ExpiredRequests);
        Assert.Equal(now, grants[0].ExpiredAt!.Value, Precision);
        Assert.Equal((null, null), (grants[1].ExpiredAt, grants[1].ExpiredRequests));
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task SpentGrantExpiresWithNothingLeft()
    {
        var now = App.Clock.GetUtcNow();
        var key = await KeyWithBalanceAsync(3_000);
        await App.SeedGrantsAsync(
            Grant(key, 5_000, now.AddMonths(-13)),
            Grant(key, 5_000, now.AddMonths(-1)));

        await RunAsync();

        Assert.Equal(3_000, (await App.KeyAsync(key.Id)).CreditsBalance);
        var spent = (await App.GrantsOfAsync(key.Id))[0];
        Assert.Equal(0, spent.ExpiredRequests);
        Assert.NotNull(spent.ExpiredAt);
        Assert.Empty(App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task GrantsDueTogetherShareTheSameBalance()
    {
        var now = App.Clock.GetUtcNow();
        var key = await KeyWithBalanceAsync(6_000);
        await App.SeedGrantsAsync(
            Grant(key, 5_000, now.AddMonths(-14)),
            Grant(key, 5_000, now.AddMonths(-13)));

        await RunAsync();

        Assert.Equal(0, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Equal(
            [1_000L, 5_000L],
            (await App.GrantsOfAsync(key.Id)).Select(static grant => grant.ExpiredRequests));
    }

    [Fact]
    public async Task UncoveredBalanceIsDatedThenExpiresAYearLater()
    {
        var found = App.Clock.GetUtcNow();
        var key = await KeyWithBalanceAsync(2_000);

        await RunAsync();

        var dated = Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(
            (ApiCreditGrantSource.Reconciliation, 2_000L, (string?)null),
            (dated.Source, dated.Requests, dated.StripeSessionId));
        Assert.Equal(found, dated.PurchasedAt, Precision);
        Assert.Equal(found.AddMonths(12), dated.ExpiresAt, Precision);
        Assert.Equal(2_000, (await App.KeyAsync(key.Id)).CreditsBalance);

        App.Clock.SetUtcNow(found.AddMonths(12).AddMinutes(1));
        await RunAsync();

        Assert.Equal(0, (await App.KeyAsync(key.Id)).CreditsBalance);
        Assert.Equal(2_000, Assert.Single(await App.GrantsOfAsync(key.Id)).ExpiredRequests);
    }

    [Fact]
    public async Task RevokedKeyIsNotDated()
    {
        var owner = await App.SeedUserAsync("Ancien");
        var key = await App.SeedKeyAsync(owner, static key =>
        {
            key.CreditsBalance = 2_000;
            key.IsActive = false;
        });

        await RunAsync();

        Assert.Empty(await App.GrantsOfAsync(key.Id));
        Assert.Equal(2_000, (await App.KeyAsync(key.Id)).CreditsBalance);
    }

    [Fact]
    public async Task PackPaidThroughStripeExpiresTwelveMonthsLater()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer);
        var paidAt = App.Clock.GetUtcNow().ToUnixTimeSeconds();
        var stripeEvent = StripePayloads.Completed(
            "evt_pack",
            StripePayloads.Pack("cs_test_pack", buyer.Id, 5_000));
        stripeEvent["created"] = paidAt;
        using var paid = await App.DeliverAsync(stripeEvent);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var due = DateTimeOffset.FromUnixTimeSeconds(paidAt).AddMonths(12);

        App.Clock.SetUtcNow(due.AddMinutes(-1));
        await RunAsync();
        var before = (await App.KeyAsync(key.Id)).CreditsBalance;
        App.Clock.SetUtcNow(due.AddDays(1));
        await RunAsync();

        Assert.Equal(5_000, before);
        Assert.Equal(0, (await App.KeyAsync(key.Id)).CreditsBalance);
    }

    [Fact]
    public async Task ExpiryRunsOnceADay()
    {
        var job = ActivatorUtilities.CreateInstance<CreditExpiryJob>(App.Services);

        var first = await job.RunIfDueAsync(Cancellation);
        var early = await job.RunIfDueAsync(Cancellation);
        App.Clock.Advance(TimeSpan.FromDays(1));
        var next = await job.RunIfDueAsync(Cancellation);

        Assert.Equal(
            (JobRunOutcome.Succeeded, JobRunOutcome.NotDue, JobRunOutcome.Succeeded),
            (first, early, next));
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static ApiCreditGrant Grant(ApiKey key, long requests, DateTimeOffset purchasedAt) =>
        new()
        {
            ApiKeyId = key.Id,
            Source = ApiCreditGrantSource.Migration,
            Requests = requests,
            PurchasedAt = purchasedAt,
            ExpiresAt = purchasedAt.AddMonths(ApiCreditGrant.ValidityMonths),
        };

    private async Task<ApiKey> KeyWithBalanceAsync(long balance)
    {
        var owner = await App.SeedUserAsync("Detenteur");
        return await App.SeedKeyAsync(owner, key =>
        {
            key.CreditsBalance = balance;
            key.RateLimitPerMin = 60;
        });
    }

    // A job of its own each run: its state row says when it last ran.
    private Task<JobRunOutcome> RunAsync() =>
        ActivatorUtilities.CreateInstance<CreditExpiryJob>(App.Services)
            .RunIfDueAsync(Cancellation);
}
