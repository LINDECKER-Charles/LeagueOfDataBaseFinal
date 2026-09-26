using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Usage;

/// <summary>The body of <c>GET /v1/usage</c>: the plan of the key and its consumption.</summary>
/// <param name="Plan">free, credits, monthly, monthly_plus, annual or annual_plus.</param>
/// <param name="MonthlyQuota">Requests the plan pays for each UTC month.</param>
/// <param name="UsedThisMonth">
/// Billed requests of the month, credits included, as written by the last flush.
/// </param>
/// <param name="RemainingThisMonth">What is left of the quota, zero at least.</param>
/// <param name="CreditsBalance">Prepaid requests left, spent once the quota is.</param>
/// <param name="RateLimitPerMin">Requests allowed per minute, in bursts of as many.</param>
internal sealed record UsageResponse(
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("monthly_quota")] long MonthlyQuota,
    [property: JsonPropertyName("used_this_month")] long UsedThisMonth,
    [property: JsonPropertyName("remaining_this_month")] long RemainingThisMonth,
    [property: JsonPropertyName("credits_balance")] long CreditsBalance,
    [property: JsonPropertyName("rate_limit_per_min")] int RateLimitPerMin);
