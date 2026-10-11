using LoDb.Api.Modules.Billing.Webhooks;
using Newtonsoft.Json.Linq;
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
    // Where an invoice named its subscription before Stripe's API version 2025-03-31, which
    // the webhook endpoint may still render its events with.
    private const string LegacySubscriptionField = "subscription";

    public string EventType => EventTypes.InvoicePaymentFailed;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        var subscriptionId = SubscriptionOf(stripeEvent.Data);
        if (string.IsNullOrEmpty(subscriptionId))
        {
            LogNoSubscription(logger, stripeEvent.Id);
            return Task.CompletedTask;
        }

        return entitlements.SuspendAsync(
            new SubscriptionEvent(subscriptionId, StripeTimes.CreatedAt(stripeEvent)),
            cancellationToken);
    }

    private static string? SubscriptionOf(EventData? data) =>
        (data?.Object as Invoice)?.Parent?.SubscriptionDetails?.SubscriptionId
        ?? LegacySubscriptionOf(data);

    // Stripe.net no longer reads the older field: it is taken from the untyped invoice.
    private static string? LegacySubscriptionOf(EventData? data)
    {
        object? invoice = data?.RawObject;
        return (invoice as JObject)?[LegacySubscriptionField] is JValue
        {
            Type: JTokenType.String,
        } subscription
            ? (string?)subscription
            : null;
    }

    [LoggerMessage(
        EventName = "billing.invoice.no_subscription",
        Level = LogLevel.Information,
        Message = "The failed invoice of the event {EventId} bills no subscription: ignored.")]
    private static partial void LogNoSubscription(ILogger logger, string eventId);
}
