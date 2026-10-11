using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;

namespace LoDb.Api.Modules.Billing.Revocation;

/// <summary>
/// <c>charge.dispute.created</c>: a buyer disputes a payment, whose funds Stripe withdraws at
/// once. What it granted is taken back, as for a full refund.
/// </summary>
/// <remarks>
/// A dispute won later gives nothing back by itself: the owner credits it by hand.
/// </remarks>
internal sealed partial class DisputeCreatedHandler(
    PaymentRevocation revocation,
    ILogger<DisputeCreatedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.ChargeDisputeCreated;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        if (stripeEvent.Data?.Object is not Dispute dispute)
        {
            LogNoDispute(logger, stripeEvent.Id);
            return Task.CompletedTask;
        }

        return revocation.RevokeAsync(
            new PaymentEvent
            {
                Cause = PaymentRevocation.Dispute,
                PaymentIntentId = dispute.PaymentIntentId,
                At = StripeTimes.CreatedAt(stripeEvent),
            },
            cancellationToken);
    }

    [LoggerMessage(
        EventName = "billing.dispute.no_dispute",
        Level = LogLevel.Warning,
        Message = "The event {EventId} carries no dispute: ignored.")]
    private static partial void LogNoDispute(ILogger logger, string eventId);
}
