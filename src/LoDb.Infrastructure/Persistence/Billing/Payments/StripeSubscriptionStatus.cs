namespace LoDb.Infrastructure.Persistence.Billing.Payments;

/// <summary>
/// Whether the key of a subscription holds its plan, stored as <c>entitled</c>,
/// <c>suspended</c> or <c>ended</c>.
/// </summary>
public enum StripeSubscriptionStatus
{
    /// <summary>Paid up: its key holds the plan.</summary>
    Entitled,

    /// <summary>
    /// Unpaid, refunded or otherwise lapsed: its key is back on the free plan until Stripe
    /// reports it active again.
    /// </summary>
    Suspended,

    /// <summary>Deleted on Stripe's side: its key let it go for good.</summary>
    Ended,
}
