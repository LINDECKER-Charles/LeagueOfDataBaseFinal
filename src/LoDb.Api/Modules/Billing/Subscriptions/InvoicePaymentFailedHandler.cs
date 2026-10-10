using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;

namespace LoDb.Api.Modules.Billing.Subscriptions;

/// <summary>
/// <c>invoice.payment_failed</c>: a subscription's invoice could not be charged. Its key goes
/// back to the free plan at once, as when Stripe marks the subscription past due, until a
/// retry or another card pays it and Stripe reports it active again.
/// </summary>
/// <remarks>An invoice of no subscription changes nothing.</remarks>
internal sealed partial class InvoicePaymentFailedHandler(
    SubscriptionEntitlements entitlements,
    ILogger<InvoicePaymentFailedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.InvoicePaymentFailed;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        var invoice = stripeEvent.Data?.Object as Invoice;
        var subscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId;
        if (string.IsNullOrEmpty(subscriptionId))
        {
            LogNoSubscription(logger, stripeEvent.Id);
            return Task.CompletedTask;
        }

        return entitlements.SuspendAsync(
            new SubscriptionEvent(subscriptionId, StripeTimes.CreatedAt(stripeEvent)),
            cancellationToken);
    }

    [LoggerMessage(
        EventName = "billing.invoice.no_subscription",
        Level = LogLevel.Information,
        Message = "The failed invoice of the event {EventId} bills no subscription: ignored.")]
    private static partial void LogNoSubscription(ILogger logger, string eventId);
}
