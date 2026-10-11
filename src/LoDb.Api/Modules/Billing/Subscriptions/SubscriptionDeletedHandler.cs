using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;

namespace LoDb.Api.Modules.Billing.Subscriptions;

/// <summary>
/// <c>customer.subscription.deleted</c>: the subscription ended, cancelled or unpaid, and its
/// key goes back to the free plan for good.
/// </summary>
/// <remarks>
/// Its credits stay, with their rate floor; the customer id stays as a trail, only the ended
/// subscription is let go.
/// </remarks>
internal sealed partial class SubscriptionDeletedHandler(
    SubscriptionEntitlements entitlements,
    ILogger<SubscriptionDeletedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.CustomerSubscriptionDeleted;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        if (stripeEvent.Data?.Object is not Subscription { Id.Length: > 0 } subscription)
        {
            LogNoSubscription(logger, stripeEvent.Id);
            return Task.CompletedTask;
        }

        return entitlements.EndAsync(
            new SubscriptionEvent(subscription.Id, StripeTimes.CreatedAt(stripeEvent)),
            cancellationToken);
    }

    [LoggerMessage(
        EventName = "billing.subscription_end.no_subscription",
        Level = LogLevel.Warning,
        Message = "The event {EventId} carries no subscription: ignored.")]
    private static partial void LogNoSubscription(ILogger logger, string eventId);
}
