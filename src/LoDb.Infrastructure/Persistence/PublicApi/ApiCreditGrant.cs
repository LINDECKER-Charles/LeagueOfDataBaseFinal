namespace LoDb.Infrastructure.Persistence.PublicApi;

/// <summary>
/// A row of <c>api_credit_grants</c>: requests added to the balance of a key, valid twelve
/// months.
/// </summary>
/// <remarks>
/// <c>api_keys.credits_balance</c> stays the counter of reference, spent by the public API
/// and read by the legacy stack; the grants only say which part of it expires when (see
/// <see cref="ApiCreditFifo"/>). A regenerated key takes the grants of the revoked one with
/// its balance.
/// </remarks>
public sealed class ApiCreditGrant
{
    /// <summary>Validity of a grant, from its purchase.</summary>
    public const int ValidityMonths = 12;

    public long Id { get; set; }

    public int ApiKeyId { get; set; }

    public ApiKey? ApiKey { get; set; }

    public ApiCreditGrantSource Source { get; set; }

    /// <summary>Requests granted, positive.</summary>
    public long Requests { get; set; }

    public DateTimeOffset PurchasedAt { get; set; }

    /// <summary><see cref="PurchasedAt"/> plus <see cref="ValidityMonths"/>.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The Checkout session of a purchase, unique; null for any other source.</summary>
    public string? StripeSessionId { get; set; }

    /// <summary>When the expiry job settled the grant; null while it is live.</summary>
    public DateTimeOffset? ExpiredAt { get; set; }

    /// <summary>Requests the expiry took off the balance, set with the date.</summary>
    public long? ExpiredRequests { get; set; }
}
