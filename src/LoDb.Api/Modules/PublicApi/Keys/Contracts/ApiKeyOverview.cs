namespace LoDb.Api.Modules.PublicApi.Keys.Contracts;

/// <summary>
/// The active key of an account at a glance: what identifies it, its rights, and what it
/// used, never its secret.
/// </summary>
internal sealed record ApiKeyOverview
{
    /// <summary>The start of the secret, such as <c>lodb_0123456</c>, to recognise it.</summary>
    public required string Prefix { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>free, credits, monthly, monthly_plus, annual or annual_plus.</summary>
    public required string Plan { get; init; }

    /// <summary>Requests the plan pays for each UTC month.</summary>
    public required int MonthlyQuota { get; init; }

    /// <summary>Billed requests of the UTC month, as metered by the last flush.</summary>
    public required long UsedThisMonth { get; init; }

    /// <summary>What is left of the quota, zero at least.</summary>
    public required long RemainingThisMonth { get; init; }

    /// <summary>Prepaid requests left, spent once the quota is.</summary>
    public required long CreditsBalance { get; init; }

    public required int RateLimitPerMin { get; init; }

    /// <summary>Whether a Stripe subscription pays for the plan.</summary>
    public required bool Subscribed { get; init; }

    /// <summary>The metered days of the last 30, newest first; a quiet day has none.</summary>
    public required IReadOnlyList<ApiUsageDay> Usage { get; init; }
}
