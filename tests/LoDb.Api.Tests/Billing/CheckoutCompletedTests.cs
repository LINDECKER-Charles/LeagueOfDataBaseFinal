using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// <c>checkout.session.completed</c>, by the <c>kind</c> of its session: a pack credits the
/// buyer's key and dates a grant, a plan subscribes it, a donation is recorded and makes a
/// signed-in donor a supporter; a session without kind is a donation, as the legacy stack
/// read it, and a kind the site does not sell is acknowledged and left alone.
/// </summary>
public sealed class CheckoutCompletedTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PackCreditsTheActiveKeyAndDatesItsGrant()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var key = await App.SeedKeyAsync(buyer, static key => key.CreditsBalance = 100);

        await DeliverAsync(StripePayloads.Pack("cs_test_pack", buyer.Id, 10_000));

        var credited = await App.KeyAsync(key.Id);
        Assert.Equal(
            (10_100L, 60, "free"),
            (credited.CreditsBalance, credited.RateLimitPerMin, credited.Plan));
        var grant = Assert.Single(await App.GrantsOfAsync(key.Id));
        Assert.Equal(
            (ApiCreditGrantSource.Purchase, 10_000L, "cs_test_pack"),
            (grant.Source, grant.Requests, grant.StripeSessionId));
        Assert.Equal(StripePayloads.CreatedAt, grant.PurchasedAt);
        Assert.Equal(StripePayloads.CreatedAt.AddMonths(12), grant.ExpiresAt);
        Assert.Null(grant.ExpiredAt);
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task PackKeepsTheHigherRateOfAPlan()
    {
        var buyer = await App.SeedUserAsync("Abonne");
        var key = await App.SeedKeyAsync(buyer, static key => key.RateLimitPerMin = 300);

        await DeliverAsync(StripePayloads.Pack("cs_test_pack", buyer.Id, 5_000));

        Assert.Equal(300, (await App.KeyAsync(key.Id)).RateLimitPerMin);
    }

    [Fact]
    public async Task PackOfABuyerWithoutKeyIssuesOne()
    {
        var buyer = await App.SeedUserAsync("SansCle");
        await App.SeedKeyAsync(buyer, static key => key.IsActive = false);

        await DeliverAsync(StripePayloads.Pack("cs_test_pack", buyer.Id, 20_000));

        var issued = (await App.KeysOfAsync(buyer.Id))[^1];
        Assert.True(issued.IsActive);
        Assert.Equal(("default", "free", 500), (issued.Name, issued.Plan, issued.MonthlyQuota));
        Assert.Equal((20_000L, 60), (issued.CreditsBalance, issued.RateLimitPerMin));
        Assert.StartsWith("lodb_", issued.KeyPrefix, StringComparison.Ordinal);
        Assert.Equal(12, issued.KeyPrefix.Length);
        Assert.Matches("^[0-9a-f]{64}$", issued.KeyHash);
        var entry = Assert.Single(await App.AuditAsync());
        Assert.Equal(
            (AuditAction.ApiKeyCreate, AuditActorType.User, (int?)buyer.Id),
            (entry.Action, entry.ActorType, entry.ActorId));
        Assert.Equal((AuditTargetType.ApiKey, Text(issued.Id)), (entry.TargetType, entry.TargetId));
        Assert.Equal("stripe", AccountsApp.Meta(entry, "source"));
    }

    [Fact]
    public async Task PackOfAnUnknownBuyerIsAcknowledgedAndCreditsNobody()
    {
        await DeliverAsync(StripePayloads.Pack("cs_test_pack", 424_242, 5_000));

        await using var db = App.Database();
        Assert.Empty(await db.ApiKeys.ToListAsync(Cancellation));
        Assert.Equal(StripeEventStatus.Processed, (await db.StripeEvents.SingleAsync(
            Cancellation)).Status);
    }

    [Theory]
    [InlineData("monthly", 15_000, 120)]
    [InlineData("monthly_plus", 45_000, 120)]
    [InlineData("annual", 20_000, 300)]
    [InlineData("annual_plus", 60_000, 300)]
    public async Task PlanSubscribesTheKey(string plan, int quota, int rate)
    {
        var buyer = await App.SeedUserAsync("Abonne");
        var key = await App.SeedKeyAsync(buyer);

        await DeliverAsync(StripePayloads.Plan("cs_test_plan", buyer.Id, plan));

        var subscribed = await App.KeyAsync(key.Id);
        Assert.Equal(
            (plan, quota, rate, StripePayloads.Customer, StripePayloads.Subscription),
            (subscribed.Plan, subscribed.MonthlyQuota, subscribed.RateLimitPerMin,
                subscribed.StripeCustomerId, subscribed.StripeSubscriptionId));
        Assert.Empty(await App.GrantsOfAsync(key.Id));
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task UnknownPlanLeavesTheKeyAlone()
    {
        var buyer = await App.SeedUserAsync("Abonne");
        var key = await App.SeedKeyAsync(buyer);

        await DeliverAsync(StripePayloads.Plan("cs_test_plan", buyer.Id, "lifetime"));

        var untouched = await App.KeyAsync(key.Id);
        Assert.Equal(("free", 500), (untouched.Plan, untouched.MonthlyQuota));
        Assert.Empty(App.KeyCache.Invalidated);
    }

    [Fact]
    public async Task DonationOfASignedInDonorMakesThemASupporter()
    {
        var donor = await App.SeedUserAsync("Donatrice");

        await DeliverAsync(StripePayloads.Donation("cs_test_don", 1_500, donor.Id));

        var donation = Assert.Single(await DonationsAsync());
        Assert.Equal(
            ("cs_test_don", 1_500, "eur", (int?)donor.Id),
            (donation.StripeSessionId, donation.AmountCents, donation.Currency, donation.UserId));
        Assert.Equal(StripePayloads.CreatedAt, donation.CreatedAt);
        await using var db = App.Database();
        Assert.True((await db.Users.SingleAsync(user => user.Id == donor.Id, Cancellation))
            .IsSupporter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(424_242)]
    public async Task DonationWithoutAKnownDonorIsAnonymous(int? donorId)
    {
        await DeliverAsync(StripePayloads.Donation("cs_test_don", 300, donorId));

        Assert.Null(Assert.Single(await DonationsAsync()).UserId);
    }

    [Fact]
    public async Task SessionWithoutKindIsADonation()
    {
        var session = StripePayloads.Session("cs_test_old", new JsonObject());

        await DeliverAsync(session);

        Assert.Equal(500, Assert.Single(await DonationsAsync()).AmountCents);
    }

    [Fact]
    public async Task DonationPaidUnderTwoEventsIsRecordedOnce()
    {
        var session = StripePayloads.Donation("cs_test_don", 1_000);

        await DeliverAsync(session, "evt_1");
        await DeliverAsync(session.DeepClone().AsObject(), "evt_2");

        Assert.Single(await DonationsAsync());
    }

    [Fact]
    public async Task UnknownKindIsAcknowledgedAndLeftAlone()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        var session = StripePayloads.Session("cs_test_gift", new JsonObject
        {
            ["user_id"] = Text(buyer.Id),
            ["kind"] = "gift_card",
        });

        await DeliverAsync(session);

        Assert.Empty(await DonationsAsync());
        Assert.Empty(await App.KeysOfAsync(buyer.Id));
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private async Task DeliverAsync(JsonObject session, string eventId = "evt_completed")
    {
        using var response = await App.DeliverAsync(StripePayloads.Completed(eventId, session));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<Donation>> DonationsAsync()
    {
        await using var db = App.Database();
        return await db.Donations.AsNoTracking().ToListAsync(Cancellation);
    }
}
