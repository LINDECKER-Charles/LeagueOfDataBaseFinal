using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Billing.Donations;

/// <summary><c>GET /api/donations/options</c>: the tiers of the donation page.</summary>
internal sealed class DonationOptionsEndpoint(CheckoutOpener checkouts)
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(
                BillingRoutes.DonationOptions,
                static ([FromServices] DonationOptionsEndpoint endpoint) => endpoint.Read())
            .WithTags(BillingRoutes.DonationsTag)
            .WithName("getDonationOptions")
            .WithSummary("Lists the donation tiers, and whether donations are open.");

    public Ok<DonationOptions> Read() => TypedResults.Ok(new DonationOptions
    {
        Available = checkouts.IsOpen,
        Presets = DonationTiers.Presets,
        MinCents = DonationTiers.MinCents,
        MaxCents = DonationTiers.MaxCents,
        Currency = CheckoutSessions.Currency,
    });
}
