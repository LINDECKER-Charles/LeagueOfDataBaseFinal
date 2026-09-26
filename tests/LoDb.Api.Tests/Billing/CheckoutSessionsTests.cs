using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Domain.Languages;
using Stripe.Checkout;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// The sessions the site asks Stripe for: one line in euros named in the visitor's language,
/// the return pages of the site in that language, and the metadata the webhook reads back.
/// </summary>
public sealed class CheckoutSessionsTests
{
    private const string Origin = "https://lodb.test";
    private const int BuyerId = 42;

    [Fact]
    public void DonationOfASignedInDonorNamesThem()
    {
        var page = CheckoutPages.Donation(Origin, UiLocale.Fr);

        var session = CheckoutSessions.Donation(page, 1_500, donorId: 7);

        Assert.Equal(("payment", "donate"), (session.Mode, session.SubmitType));
        AssertLine(session, "Don — League Of Data Base", 1_500);
        Assert.Equal(
            "https://lodb.test/fr/donate/success?session_id={CHECKOUT_SESSION_ID}",
            session.SuccessUrl);
        Assert.Equal("https://lodb.test/fr/donate/cancel", session.CancelUrl);
        Assert.Equal("7", session.ClientReferenceId);
        Assert.Equal(
            new Dictionary<string, string> { ["source"] = "lodb-donate", ["kind"] = "donation" },
            session.Metadata);
    }

    [Fact]
    public void AnonymousDonationNamesNobody()
    {
        var page = CheckoutPages.Donation(Origin, UiLocale.En);

        var session = CheckoutSessions.Donation(page, 300, donorId: null);

        Assert.Null(session.ClientReferenceId);
        Assert.False(session.Metadata.ContainsKey("user_id"));
    }

    [Fact]
    public void DonationNameFallsBackToEnglish()
    {
        Assert.Equal(
            "Donation — League Of Data Base",
            CheckoutPages.Donation(Origin, UiLocale.De).ProductName);
    }

    [Theory]
    [InlineData("small", 500, "5000")]
    [InlineData("medium", 1_000, "10000")]
    [InlineData("large", 2_000, "20000")]
    public void PackNamesItsBuyerAndItsRequests(string code, long price, string requests)
    {
        var pack = ApiPacks.Find(code)!;

        var session = CheckoutSessions.Pack(CheckoutPages.Pack(Origin, UiLocale.En, pack), pack,
            BuyerId);

        Assert.Equal("payment", session.Mode);
        Assert.Null(session.SubmitType);
        AssertLine(session, $"LoDB API — {requests} request credits", price);
        Assert.Null(session.LineItems[0].PriceData.Recurring);
        Assert.Equal("https://lodb.test/en/account/api?status=pack_success", session.SuccessUrl);
        Assert.Equal("https://lodb.test/en/account/api?status=cancelled", session.CancelUrl);
        Assert.Equal("42", session.ClientReferenceId);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["user_id"] = "42",
                ["kind"] = "api_pack",
                ["requests"] = requests,
            },
            session.Metadata);
    }

    [Theory]
    [InlineData("monthly", 500, "month", "Mensuel")]
    [InlineData("monthly_plus", 1_500, "month", "Mensuel+")]
    [InlineData("annual", 4_800, "year", "Annuel")]
    [InlineData("annual_plus", 14_400, "year", "Annuel+")]
    public void PlanSubscribesAtItsInterval(string code, long price, string interval, string name)
    {
        var plan = ApiPlans.Find(code)!;

        var session = CheckoutSessions.Plan(CheckoutPages.Plan(Origin, UiLocale.Fr, plan), plan,
            BuyerId);

        Assert.Equal("subscription", session.Mode);
        AssertLine(session, $"API LoDB — offre {name}", price);
        Assert.Equal(interval, session.LineItems[0].PriceData.Recurring.Interval);
        Assert.Equal("https://lodb.test/fr/account/api?status=plan_success", session.SuccessUrl);
        Assert.Equal("https://lodb.test/fr/account/api?status=cancelled", session.CancelUrl);
        Assert.Equal("42", session.ClientReferenceId);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["user_id"] = "42",
                ["kind"] = "api_plan",
                ["plan"] = code,
            },
            session.Metadata);
    }

    [Fact]
    public void EveryLanguageNamesThePacksAndPlans()
    {
        var names = UiLocales.All.SelectMany(static locale => (IEnumerable<string>)
        [
            ProductNames.Donation(locale),
            .. ApiPacks.All.Select(pack => ProductNames.Pack(locale, pack)),
            .. ApiPlans.Subscriptions.Select(plan => ProductNames.Plan(locale, plan)),
        ]);

        Assert.All(names, static name =>
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.DoesNotContain("{{", name, StringComparison.Ordinal);
        });
    }

    private static void AssertLine(SessionCreateOptions session, string name, long amountCents)
    {
        var line = Assert.Single(session.LineItems);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(
            ("eur", amountCents, name),
            (line.PriceData.Currency, line.PriceData.UnitAmount, line.PriceData.ProductData.Name));
    }
}
