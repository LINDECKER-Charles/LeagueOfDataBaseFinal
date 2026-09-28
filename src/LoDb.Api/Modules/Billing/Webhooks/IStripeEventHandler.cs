using Stripe;

namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>Applies the events of one type, inside the transaction that records them.</summary>
/// <remarks>
/// A handler writes through the scoped context and leaves the saving to the processor. It
/// logs and returns on data a redelivery cannot fix, and throws on any other failure, so that
/// nothing is kept and Stripe delivers the event again.
/// </remarks>
internal interface IStripeEventHandler
{
    /// <summary>Stripe's type of the events handled: <c>checkout.session.completed</c>…</summary>
    string EventType { get; }

    Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken);
}
