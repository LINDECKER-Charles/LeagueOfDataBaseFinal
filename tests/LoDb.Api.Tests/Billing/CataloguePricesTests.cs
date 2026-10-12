using System.Text.Json.Nodes;
using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Payments;
using LoDb.Api.Tests.Billing.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// A paid session matches the catalogue when it charged, in euros, the price of its pack or
/// of its plan, or an amount of the donation tiers; anything else is no session the site
/// opened as such.
/// </summary>
public sealed class CataloguePricesTests
{
    private const string SessionId = "cs_test_prices";
    private const long PackOnSale = 5_000;
    private const long PackNotOnSale = 7_000;
    private const string PlanOnSale = "annual";

    // No database: the identifiers need no mark.
    private static readonly StripePayloads Stripe = new(string.Empty);

    public static TheoryData<string> CatalogueOffers =>
    [
        "pack at its price",
        "plan at its price",
        "donation of the lowest tier",
        "donation of the highest tier",
    ];

    public static TheoryData<string> OffCatalogueOffers =>
    [
        "pack in dollars",
        "pack of requests not on sale",
        "pack without amount",
        "plan below its price",
        "plan not on sale",
        "donation above the tiers",
        "donation below the tiers",
    ];

    [Theory]
    [MemberData(nameof(CatalogueOffers))]
    public void SessionAtTheCataloguePriceMatches(string offer) =>
        Assert.True(CataloguePrices.Match(Read(Offer(offer))));

    [Theory]
    [MemberData(nameof(OffCatalogueOffers))]
    public void SessionChargingAnythingElseIsNoMatch(string offer) =>
        Assert.False(CataloguePrices.Match(Read(Offer(offer))));

    private static JsonObject Offer(string offer) => offer switch
    {
        "pack at its price" => Pack(),
        "plan at its price" => Plan(),
        "donation of the lowest tier" => Donation(DonationTiers.MinCents),
        "donation of the highest tier" => Donation(DonationTiers.MaxCents),
        "pack in dollars" => With(Pack(), "currency", "usd"),
        "pack of requests not on sale" => Pack(PackNotOnSale),
        "pack without amount" => With(Pack(), "amount_total", null),
        "plan below its price" => With(Plan(), "amount_total", PlanPrice() - 1),
        "plan not on sale" => Plan("lifetime"),
        "donation above the tiers" => Donation(DonationTiers.MaxCents + 1),
        "donation below the tiers" => Donation(DonationTiers.MinCents - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(offer), offer, null),
    };

    // The session as the webhook reads it from its event.
    private static SessionEvent Read(JsonObject session)
    {
        var stripeEvent = EventUtility.ParseEvent(
            Stripe.Completed("evt_prices", session).ToJsonString(),
            throwOnApiVersionMismatch: false);
        return new SessionEventReader(NullLogger<SessionEventReader>.Instance).Read(stripeEvent)
            ?? throw new InvalidOperationException("The event carries no session the site sells.");
    }

    private static JsonObject Pack(long requests = PackOnSale) =>
        Stripe.Pack(SessionId, 0, requests);

    private static long PlanPrice() => ApiPlans.Find(PlanOnSale)!.PriceCents;

    private static JsonObject Plan(string plan = PlanOnSale) =>
        Stripe.Plan(SessionId, 0, plan);

    private static JsonObject Donation(long amountCents)
    {
        var session = Stripe.Donation(SessionId, 0);
        session["amount_total"] = amountCents;
        return session;
    }

    private static JsonObject With(JsonObject session, string field, JsonNode? value)
    {
        session[field] = value;
        return session;
    }
}
