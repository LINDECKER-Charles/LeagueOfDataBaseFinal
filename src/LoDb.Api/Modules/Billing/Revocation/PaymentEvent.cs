namespace LoDb.Api.Modules.Billing.Revocation;

/// <summary>A refund or a dispute, by the payment it takes back.</summary>
internal sealed record PaymentEvent
{
    /// <summary>
    /// <see cref="PaymentRevocation.Refund"/> or <see cref="PaymentRevocation.Dispute"/>.
    /// </summary>
    public required string Cause { get; init; }

    /// <summary>The payment, which names the session that sold it; null when absent.</summary>
    public string? PaymentIntentId { get; init; }

    /// <summary>Its customer, which names the key of a subscription; null when absent.</summary>
    public string? CustomerId { get; init; }

    /// <summary>When Stripe created the event.</summary>
    public required DateTimeOffset At { get; init; }
}
