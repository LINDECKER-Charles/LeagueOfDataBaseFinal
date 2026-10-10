using LoDb.Infrastructure.Persistence.Billing.Payments;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Payments;

/// <summary>A Checkout session as one of Stripe's events tells it.</summary>
/// <param name="Session">The session, as the event carries it.</param>
/// <param name="Kind">What it sold.</param>
/// <param name="At">
/// When Stripe created the event: when the session completed, was paid or failed.
/// </param>
internal sealed record SessionEvent(Session Session, StripeCheckoutKind Kind, DateTimeOffset At);
