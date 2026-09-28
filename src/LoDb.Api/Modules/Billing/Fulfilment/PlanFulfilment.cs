using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// A subscription taken (<c>kind: api_plan</c>): the buyer's key gets the plan's quota and
/// rate, the credits floor kept, and the Stripe ids its end will be matched by.
/// </summary>
internal sealed partial class PlanFulfilment(
    LoDbDbContext db,
    BuyerKeys keys,
    BillingEffects effects,
    ILogger<PlanFulfilment> logger)
{
    public async Task ApplyAsync(Session session, CancellationToken cancellationToken)
    {
        if (ApiPlans.Find(SessionFields.Metadata(session, CheckoutMetadata.Plan)) is not { } plan)
        {
            LogUnknownPlan(logger, session.Id);
            return;
        }

        if (await keys.ActiveKeyAsync(SessionFields.BuyerId(session), cancellationToken)
            is not { } key)
        {
            return;
        }

        await db.ApiKeys
            .Where(row => row.Id == key.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Plan, plan.Code)
                    .SetProperty(row => row.MonthlyQuota, plan.MonthlyQuota)
                    .SetProperty(
                        row => row.RateLimitPerMin,
                        row => row.CreditsBalance > 0
                            ? Math.Max(plan.RatePerMinute, ApiPlans.CreditsRate)
                            : plan.RatePerMinute)
                    .SetProperty(row => row.StripeCustomerId, session.CustomerId)
                    .SetProperty(row => row.StripeSubscriptionId, session.SubscriptionId),
                cancellationToken);
        effects.KeyChanged(key);
        LogSubscribed(logger, key.Id, plan.Code);
    }

    [LoggerMessage(
        EventName = "billing.plan.subscribed",
        Level = LogLevel.Information,
        Message = "The API key {ApiKeyId} moved to the plan {Plan}.")]
    private static partial void LogSubscribed(ILogger logger, int apiKeyId, string plan);

    [LoggerMessage(
        EventName = "billing.plan.unknown",
        Level = LogLevel.Warning,
        Message = "The plan session {SessionId} names no plan on sale: nothing applied.")]
    private static partial void LogUnknownPlan(ILogger logger, string sessionId);
}
