using LoDb.Api.Modules.Billing.Fulfilment;
using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// The real handler of completed sessions, failing once armed after it has written: what
/// it wrote must go with the event's row.
/// </summary>
internal sealed class FaultyCheckoutHandler(CheckoutCompletedHandler inner, HandlerFault fault)
    : IStripeEventHandler
{
    public string EventType => inner.EventType;

    public async Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        await inner.HandleAsync(stripeEvent, cancellationToken);
        fault.ThrowIfArmed();
    }
}
