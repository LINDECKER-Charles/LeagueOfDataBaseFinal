using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// The purchases of the API portal: the offers are public; a pack or a plan is bought by a
/// signed-in account onto its active key, a plan only once. How many an hour is
/// <see cref="OwnHost.PurchaseCheckoutLimitTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with accounts of its own.</remarks>
public sealed class PurchaseCheckoutTests : IClassFixture<SharedBillingApp>
{
    private const string OffersPath = "/api/billing/offers";
    private const string PackPath = "/api/billing/checkout/pack";
    private const string PlanPath = "/api/billing/checkout/plan";

    public PurchaseCheckoutTests(SharedBillingApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        App.ResetDoubles();
    }

    private BillingApp App { get; }

    [Fact]
    public async Task OffersArePublic()
    {
        using var browser = App.Browser();

        using var response = await browser.GetAsync(OffersPath);

        var offers = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.True(offers.GetProperty("available").GetBoolean());
        Assert.Equal(["small", "medium", "large"], Codes(offers.GetProperty("packs")));
        Assert.Equal(
            ["monthly", "monthly_plus", "annual", "annual_plus"],
            Codes(offers.GetProperty("plans")));
    }

    [Theory]
    [InlineData(PackPath)]
    [InlineData(PlanPath)]
    public async Task VisitorMustSignIn(string path)
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(path, new { pack = "small", plan = "annual" });

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Unauthorized));
    }

    [Theory]
    [InlineData(PackPath)]
    [InlineData(PlanPath)]
    public async Task PurchaseNeedsAnActiveKey(string path)
    {
        using var browser = await SignedInAsync(await App.SeedUserAsync("SansCle"));

        using var response = await browser.PostAsync(path, new { pack = "small", plan = "annual" });

        Assert.Equal(
            "api-key-required",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Conflict));
        Assert.Empty(App.Gateway.Sessions);
    }

    [Fact]
    public async Task PackIsSoldOntoTheBuyersKey()
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        await App.SeedKeyAsync(buyer);
        using var browser = await SignedInAsync(buyer);

        using var response = await browser.PostAsync(
            PackPath,
            new { pack = "medium", locale = "de" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = Assert.Single(App.Gateway.Sessions);
        var line = session.LineItems[0].PriceData;
        Assert.Equal(("payment", 1_000L), (session.Mode, line.UnitAmount));
        var metadata = session.Metadata;
        Assert.Equal(Text(buyer.Id), metadata["user_id"]);
        Assert.Equal(("api_pack", "10000"), (metadata["kind"], metadata["requests"]));
        Assert.Equal(
            AccountsApp.SiteOrigin + "/de/account/api?status=pack_success",
            session.SuccessUrl);
    }

    [Fact]
    public async Task PlanIsSoldAsASubscription()
    {
        var buyer = await App.SeedUserAsync("Abonne");
        await App.SeedKeyAsync(buyer);
        using var browser = await SignedInAsync(buyer);

        using var response = await browser.PostAsync(PlanPath, new { plan = "annual_plus" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = Assert.Single(App.Gateway.Sessions);
        Assert.Equal("subscription", session.Mode);
        Assert.Equal("year", session.LineItems[0].PriceData.Recurring.Interval);
        var metadata = session.Metadata;
        Assert.Equal(("api_plan", "annual_plus"), (metadata["kind"], metadata["plan"]));
    }

    [Fact]
    public async Task SubscribedKeyIsNotSoldASecondPlan()
    {
        var buyer = await App.SeedUserAsync("Abonne");
        await App.SeedKeyAsync(buyer, static key => key.StripeSubscriptionId = "sub_test_plan");
        using var browser = await SignedInAsync(buyer);

        using var response = await browser.PostAsync(PlanPath, new { plan = "monthly" });

        Assert.Equal(
            "already-subscribed",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData(PackPath, "pack", "huge")]
    [InlineData(PlanPath, "plan", "lifetime")]
    public async Task UnknownOfferIsRefused(string path, string field, string code)
    {
        var buyer = await App.SeedUserAsync("Acheteur");
        await App.SeedKeyAsync(buyer);
        using var browser = await SignedInAsync(buyer);

        using var response = await browser.PostAsync(path, new Dictionary<string, string>
        {
            [field] = code,
        });

        Assert.Equal(["invalid"], (await ApiJson.FieldErrorsAsync(response))[field]);
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<string?> Codes(JsonElement offers) =>
        offers.EnumerateArray().Select(static offer => ApiJson.Text(offer, "code"));

    private async Task<BrowserClient> SignedInAsync(User account)
    {
        var browser = App.Browser();
        using var response = await browser.SignInAsync(account.UserName!);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return browser;
    }
}
