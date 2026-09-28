using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Billing.Purchases;

/// <summary>
/// <c>GET /api/billing/offers</c>: the packs and plans on sale, for the API portal and the
/// pricing of <c>/developers</c>.
/// </summary>
internal sealed class OffersEndpoint(CheckoutOpener checkouts)
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(
                BillingRoutes.Offers,
                static ([FromServices] OffersEndpoint endpoint) => endpoint.Read())
            .WithTags(BillingRoutes.BillingTag)
            .WithName("getBillingOffers")
            .WithSummary("Lists the credit packs and plans of the public API on sale.");

    public Ok<BillingOffers> Read() => TypedResults.Ok(new BillingOffers
    {
        Available = checkouts.IsOpen,
        Currency = CheckoutSessions.Currency,
        Packs = ApiPacks.All,
        Plans = ApiPlans.Subscriptions,
    });
}
