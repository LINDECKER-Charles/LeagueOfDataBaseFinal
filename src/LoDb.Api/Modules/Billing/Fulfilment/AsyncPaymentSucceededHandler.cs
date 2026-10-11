using LoDb.Api.Modules.Billing.Payments;
using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// <c>checkout.session.async_payment_succeeded</c>: the delayed payment of a completed session
/// came in, and the session is granted as one paid at once.
/// </summary>
internal sealed class AsyncPaymentSucceededHandler(
    SessionEventReader reader,
    SessionFulfilment fulfilment) : IStripeEventHandler
{
    public string EventType => EventTypes.CheckoutSessionAsyncPaymentSucceeded;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken) =>
        reader.Read(stripeEvent) is { } paid
            ? fulfilment.FulfilAsync(paid, cancellationToken)
            : Task.CompletedTask;
}
