using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Api.Modules.Billing.Subscriptions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// A subscription taken (<c>kind: api_plan</c>): the buyer's key gets the plan's quota and
/// rate, the credits floor kept, and the Stripe ids its end will be matched by; the
/// subscription is recorded with the plan it pays for.
/// </summary>
internal sealed partial class PlanFulfilment(
    LoDbDbContext db,
    BuyerKeys keys,
    BillingEffects effects,
    SubscriptionEntitlements subscriptions,
    ILogger<PlanFulfilment> logger)
{
    /// <param name="session">The paid session.</param>
    /// <param name="paidAt">When it was paid: the later events of its subscription count.</param>
    /// <param name="cancellationToken">Cancels the writes, rolled back with the event.</param>
    public async Task ApplyAsync(
        Session session,
        DateTimeOffset paidAt,
        CancellationToken cancellationToken)
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
            .ExecuteUpdateAsync(Subscribe(plan, session), cancellationToken);
        effects.KeyChanged(key);
        if (!string.IsNullOrEmpty(session.SubscriptionId))
        {
            await subscriptions.EntitleAsync(
                new SubscriptionEvent(session.SubscriptionId, paidAt),
                plan.Code,
                cancellationToken);
        }

        LogSubscribed(logger, key.Id, plan.Code);
    }

    private static Action<UpdateSettersBuilder<ApiKey>> Subscribe(
        ApiPlanTerms plan,
        Session session) =>
        setters => setters
            .SetProperty(row => row.Plan, plan.Code)
            .SetProperty(row => row.MonthlyQuota, plan.MonthlyQuota)
            .SetProperty(
                row => row.RateLimitPerMin,
                row => row.CreditsBalance > 0
                    ? Math.Max(plan.RatePerMinute, ApiPlans.CreditsRate)
                    : plan.RatePerMinute)
            .SetProperty(row => row.StripeCustomerId, session.CustomerId)
            .SetProperty(row => row.StripeSubscriptionId, session.SubscriptionId);

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
