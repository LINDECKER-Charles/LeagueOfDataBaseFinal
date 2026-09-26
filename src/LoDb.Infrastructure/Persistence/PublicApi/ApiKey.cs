using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Infrastructure.Persistence.PublicApi;

/// <summary>
/// A row of <c>api_keys</c>. Its column names are a contract of the public API (go-api
/// reads them), kept as they are until the cutover.
/// </summary>
public sealed class ApiKey
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>SHA-256 of the whole key, in hexadecimal; the key itself is never stored.</summary>
    public required string KeyHash { get; set; }

    /// <summary>Displayable start of the key, such as <c>lodb_ab12</c>.</summary>
    public required string KeyPrefix { get; set; }

    /// <summary>free, credits, monthly, monthly_plus, annual or annual_plus.</summary>
    public required string Plan { get; set; }

    public int MonthlyQuota { get; set; }

    /// <summary>Prepaid requests, spent once the monthly quota is used up.</summary>
    public long CreditsBalance { get; set; }

    public int RateLimitPerMin { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? StripeCustomerId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }
}
