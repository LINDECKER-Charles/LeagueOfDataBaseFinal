namespace LoDb.Infrastructure.Persistence.Billing.Payments;

/// <summary>
/// Where a Checkout session stands, stored as <c>pending</c>, <c>paid</c>, <c>failed</c>,
/// <c>refused</c> or <c>revoked</c>.
/// </summary>
public enum StripeCheckoutStatus
{
    /// <summary>Completed while its payment is still on its way: nothing granted yet.</summary>
    Pending,

    /// <summary>Paid the catalogue's price: what it sold was granted.</summary>
    Paid,

    /// <summary>Its delayed payment failed: nothing granted.</summary>
    Failed,

    /// <summary>Paid another amount or currency than the catalogue's: nothing granted.</summary>
    Refused,

    /// <summary>Refunded in full or disputed once paid: what it granted was taken back.</summary>
    Revoked,
}
