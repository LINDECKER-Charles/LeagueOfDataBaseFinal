namespace LoDb.Infrastructure.Persistence.Billing.Payments;

/// <summary>
/// A row of <c>stripe_checkout</c>: a Checkout session the webhook received, and what became of
/// it.
/// </summary>
/// <remarks>
/// Written by the webhook only, in the transaction of the event that changes it. A session paid
/// before the table existed has no row: its grant or its donation, unique by session, still
/// keeps it from being fulfilled twice.
/// </remarks>
public sealed class StripeCheckout
{
    /// <summary>Stripe's id of the session, such as <c>cs_live_a1…</c>.</summary>
    public required string Id { get; set; }

    public StripeCheckoutKind Kind { get; set; }

    public StripeCheckoutStatus Status { get; set; }

    /// <summary>
    /// The payment of a session in payment mode, which refunds and disputes name; null in
    /// subscription mode, whose payments belong to invoices.
    /// </summary>
    public string? PaymentIntentId { get; set; }

    /// <summary>What Stripe charged, in the smallest unit of <see cref="Currency"/>.</summary>
    public long AmountCents { get; set; }

    /// <summary>
    /// ISO 4217 code, lowercase as Stripe writes it; empty when the session has none.
    /// </summary>
    public required string Currency { get; set; }

    /// <summary>When Stripe created the first event received about the session.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When Stripe created the event that set <see cref="Status"/>.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
