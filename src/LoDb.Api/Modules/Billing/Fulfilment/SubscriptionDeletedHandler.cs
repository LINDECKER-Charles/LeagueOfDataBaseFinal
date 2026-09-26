using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Api.Modules.Billing.Webhooks;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// <c>customer.subscription.deleted</c>: the subscription ended, cancelled or unpaid, and its
/// key goes back to the free plan.
/// </summary>
/// <remarks>
/// Its credits stay, with their rate floor; the customer id stays as a trail, only the ended
/// subscription is let go.
/// </remarks>
internal sealed partial class SubscriptionDeletedHandler(
    LoDbDbContext db,
    BillingEffects effects,
    ILogger<SubscriptionDeletedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.CustomerSubscriptionDeleted;

    public async Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        var subscriptionId = (stripeEvent.Data?.Object as Subscription)?.Id;
        var key = string.IsNullOrEmpty(subscriptionId)
            ? null
            : await db.ApiKeys
                .Where(row => row.StripeSubscriptionId == subscriptionId && row.IsActive)
                .OrderByDescending(row => row.Id)
                .FirstOrDefaultAsync(cancellationToken);
        if (key is null)
        {
            // Revoked meanwhile, or a subscription the site never matched to a key.
            LogUnmatched(logger, stripeEvent.Id);
            return;
        }

        await db.ApiKeys
            .Where(row => row.Id == key.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Plan, ApiPlans.Free)
                    .SetProperty(row => row.MonthlyQuota, ApiPlans.FreeQuota)
                    .SetProperty(
                        row => row.RateLimitPerMin,
                        row => row.CreditsBalance > 0 ? ApiPlans.CreditsRate : ApiPlans.FreeRate)
                    .SetProperty(row => row.StripeSubscriptionId, (string?)null),
                cancellationToken);
        effects.KeyChanged(key);
        LogReleased(logger, key.Id);
    }

    [LoggerMessage(
        EventName = "billing.plan.released",
        Level = LogLevel.Information,
        Message = "The subscription of the API key {ApiKeyId} ended: back to the free plan.")]
    private static partial void LogReleased(ILogger logger, int apiKeyId);

    [LoggerMessage(
        EventName = "billing.plan.unmatched",
        Level = LogLevel.Information,
        Message = "The ended subscription of the event {EventId} matches no active key.")]
    private static partial void LogUnmatched(ILogger logger, string eventId);
}
