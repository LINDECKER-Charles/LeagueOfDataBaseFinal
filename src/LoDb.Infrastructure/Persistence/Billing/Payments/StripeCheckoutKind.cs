namespace LoDb.Infrastructure.Persistence.Billing.Payments;

/// <summary>
/// What a Checkout session sold, stored as the <c>kind</c> of its metadata: <c>api_pack</c>,
/// <c>api_plan</c> or <c>donation</c>.
/// </summary>
public enum StripeCheckoutKind
{
    /// <summary>A credit pack of the public API.</summary>
    Pack,

    /// <summary>A subscription to a plan of the public API.</summary>
    Plan,

    /// <summary>A donation, which a session without any kind is too.</summary>
    Donation,
}
