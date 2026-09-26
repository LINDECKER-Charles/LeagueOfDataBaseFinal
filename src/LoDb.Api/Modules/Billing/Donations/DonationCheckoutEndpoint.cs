using LoDb.Api.Hosting;
using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Billing.Donations;

/// <summary>
/// <c>POST /api/donations/checkout</c>: opens the Checkout session of a donation and returns
/// Stripe's page, which the client navigates to.
/// </summary>
/// <remarks>
/// Open to visitors, behind the forgery guard and the <c>donation-checkout</c> rate limit. A
/// signed-in donor is named in the session, so that the webhook makes them a supporter.
/// </remarks>
internal sealed class DonationCheckoutEndpoint(CheckoutOpener checkouts, BillingAccounts accounts)
{
    private const string AmountField = "amountCents";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                BillingRoutes.DonationCheckout,
                static (
                    [FromBody] DonationCheckoutRequest request,
                    [FromServices] DonationCheckoutEndpoint endpoint,
                    HttpContext context) => endpoint.OpenAsync(request, context))
            .WithTags(BillingRoutes.DonationsTag)
            .RequireRateLimiting(RateLimitingPolicies.DonationCheckout)
            .WithName("openDonationCheckout")
            .WithSummary("Opens the Stripe checkout of a donation.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<Ok<CheckoutCreated>, BillingProblem>> OpenAsync(
        DonationCheckoutRequest request,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (checkouts.SiteOrigin() is not { } origin)
        {
            return BillingProblem.Unavailable();
        }

        if (request.AmountCents is not { } amount || !DonationTiers.Allows(amount))
        {
            return BillingProblem.InvalidField(AmountField);
        }

        var donor = await accounts.FindAsync(context.User);
        var page = CheckoutPages.Donation(origin, BillingLocale.Of(request.Locale));
        return await checkouts.OpenAsync(
            CheckoutSessions.Donation(page, amount, donor?.Id),
            CheckoutMetadata.DonationKind,
            context.RequestAborted);
    }
}
