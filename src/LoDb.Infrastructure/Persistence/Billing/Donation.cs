using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Infrastructure.Persistence.Billing;

/// <summary>A row of <c>donations</c>: a completed Stripe Checkout session.</summary>
public sealed class Donation
{
    public int Id { get; set; }

    public required string StripeSessionId { get; set; }

    public int AmountCents { get; set; }

    /// <summary>ISO 4217 code, three letters.</summary>
    public required string Currency { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Null for an anonymous donor or a deleted account.</summary>
    public int? UserId { get; set; }

    public User? User { get; set; }
}
