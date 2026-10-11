using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;

namespace LoDb.Api.Modules.Billing.Subscriptions;

/// <summary>
/// <c>customer.subscription.updated</c>: the key follows the status of its subscription, on
/// the free plan while it is past due, unpaid, paused or over, its plan back once it is active
/// again.
/// </summary>
internal sealed partial class SubscriptionUpdatedHandler(
    SubscriptionEntitlements entitlements,
    ILogger<SubscriptionUpdatedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.CustomerSubscriptionUpdated;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        if (stripeEvent.Data?.Object is not Subscription { Id.Length: > 0 } subscription)
        {
            LogNoSubscription(logger, stripeEvent.Id);
            return Task.CompletedTask;
        }

        var change = new SubscriptionEvent(subscription.Id, StripeTimes.CreatedAt(stripeEvent));
        if (SubscriptionStatuses.IsPaidUp(subscription.Status))
        {
            return entitlements.RestoreAsync(change, cancellationToken);
        }

        return SubscriptionStatuses.IsLapsed(subscription.Status)
            ? entitlements.SuspendAsync(change, cancellationToken)
            : Task.CompletedTask;
    }

    [LoggerMessage(
        EventName = "billing.subscription_update.no_subscription",
        Level = LogLevel.Warning,
        Message = "The event {EventId} carries no subscription: ignored.")]
    private static partial void LogNoSubscription(ILogger logger, string eventId);
}
