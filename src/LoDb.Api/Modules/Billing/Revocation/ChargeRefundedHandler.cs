using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;

namespace LoDb.Api.Modules.Billing.Revocation;

/// <summary>
/// <c>charge.refunded</c>: a payment refunded in full takes back what it granted.
/// </summary>
/// <remarks>
/// A partial refund is a gesture the owner makes while the buyer keeps what they bought: it
/// takes nothing back.
/// </remarks>
internal sealed partial class ChargeRefundedHandler(
    PaymentRevocation revocation,
    ILogger<ChargeRefundedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.ChargeRefunded;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        if (stripeEvent.Data?.Object is not Charge charge)
        {
            LogNoCharge(logger, stripeEvent.Id);
            return Task.CompletedTask;
        }

        if (!charge.Refunded)
        {
            LogPartial(logger, charge.Id, charge.AmountRefunded, charge.Amount);
            return Task.CompletedTask;
        }

        return revocation.RevokeAsync(
            new PaymentEvent
            {
                Cause = PaymentRevocation.Refund,
                PaymentIntentId = charge.PaymentIntentId,
                CustomerId = charge.CustomerId,
                At = StripeTimes.CreatedAt(stripeEvent),
            },
            cancellationToken);
    }

    [LoggerMessage(
        EventName = "billing.refund.partial",
        Level = LogLevel.Information,
        Message = "The charge {ChargeId} was refunded {Refunded} of {Amount}: nothing taken "
            + "back.")]
    private static partial void LogPartial(
        ILogger logger,
        string chargeId,
        long refunded,
        long amount);

    [LoggerMessage(
        EventName = "billing.refund.no_charge",
        Level = LogLevel.Warning,
        Message = "The event {EventId} carries no charge: ignored.")]
    private static partial void LogNoCharge(ILogger logger, string eventId);
}
