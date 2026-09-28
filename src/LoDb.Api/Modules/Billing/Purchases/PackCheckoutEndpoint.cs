using LoDb.Api.Hosting;
using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Http;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Purchases;

/// <summary>
/// <c>POST /api/billing/checkout/pack</c>: opens the Checkout session of a credit pack, which
/// comes back to the API portal.
/// </summary>
/// <remarks>The credits need an active key to land on, as in the legacy portal.</remarks>
internal sealed class PackCheckoutEndpoint(
    CheckoutOpener checkouts,
    BillingAccounts accounts,
    LoDbDbContext db)
{
    private const string PackField = "pack";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                BillingRoutes.PackCheckout,
                static (
                    [FromBody] PackCheckoutRequest request,
                    [FromServices] PackCheckoutEndpoint endpoint,
                    HttpContext context) => endpoint.OpenAsync(request, context))
            .WithTags(BillingRoutes.BillingTag)
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .WithName("openPackCheckout")
            .WithSummary("Opens the Stripe checkout of a credit pack for the caller's API key.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<Ok<CheckoutCreated>, BillingProblem>> OpenAsync(
        PackCheckoutRequest request,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (checkouts.SiteOrigin() is not { } origin)
        {
            return BillingProblem.Unavailable();
        }

        if (ApiPacks.Find(request.Pack) is not { } pack)
        {
            return BillingProblem.InvalidField(PackField);
        }

        if (await accounts.FindAsync(context.User) is not { } buyer)
        {
            return BillingProblem.SignedOut();
        }

        if (!await HasKeyAsync(buyer.Id, context.RequestAborted))
        {
            return BillingProblem.ApiKeyRequired();
        }

        var page = CheckoutPages.Pack(origin, BillingLocale.Of(request.Locale), pack);
        return await checkouts.OpenAsync(
            CheckoutSessions.Pack(page, pack, buyer.Id),
            CheckoutMetadata.PackKind,
            context.RequestAborted);
    }

    private async Task<bool> HasKeyAsync(int buyerId, CancellationToken cancellationToken) =>
        await db.ApiKeys.AsNoTracking().ActiveOfAsync(buyerId, cancellationToken) is not null;
}
