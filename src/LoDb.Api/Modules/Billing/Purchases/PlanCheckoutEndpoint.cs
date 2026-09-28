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
/// <c>POST /api/billing/checkout/plan</c>: opens the Checkout session of a subscription,
/// which comes back to the API portal.
/// </summary>
/// <remarks>
/// One subscription a key: changing plans means cancelling first, as in the legacy portal.
/// </remarks>
internal sealed class PlanCheckoutEndpoint(
    CheckoutOpener checkouts,
    BillingAccounts accounts,
    LoDbDbContext db)
{
    private const string PlanField = "plan";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                BillingRoutes.PlanCheckout,
                static (
                    [FromBody] PlanCheckoutRequest request,
                    [FromServices] PlanCheckoutEndpoint endpoint,
                    HttpContext context) => endpoint.OpenAsync(request, context))
            .WithTags(BillingRoutes.BillingTag)
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .WithName("openPlanCheckout")
            .WithSummary("Opens the Stripe checkout of a subscription for the caller's API key.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<Ok<CheckoutCreated>, BillingProblem>> OpenAsync(
        PlanCheckoutRequest request,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (checkouts.SiteOrigin() is not { } origin)
        {
            return BillingProblem.Unavailable();
        }

        if (ApiPlans.Find(request.Plan) is not { } plan)
        {
            return BillingProblem.InvalidField(PlanField);
        }

        if (await accounts.FindAsync(context.User) is not { } buyer)
        {
            return BillingProblem.SignedOut();
        }

        if (await RefusalAsync(buyer.Id, context.RequestAborted) is { } refusal)
        {
            return refusal;
        }

        var page = CheckoutPages.Plan(origin, BillingLocale.Of(request.Locale), plan);
        return await checkouts.OpenAsync(
            CheckoutSessions.Plan(page, plan, buyer.Id),
            CheckoutMetadata.PlanKind,
            context.RequestAborted);
    }

    // A subscription needs a key to land on, and one without a subscription yet.
    private async Task<BillingProblem?> RefusalAsync(
        int buyerId,
        CancellationToken cancellationToken)
    {
        var key = await db.ApiKeys.AsNoTracking().ActiveOfAsync(buyerId, cancellationToken);
        if (key is null)
        {
            return BillingProblem.ApiKeyRequired();
        }

        return key.StripeSubscriptionId is null ? null : BillingProblem.AlreadySubscribed();
    }
}
