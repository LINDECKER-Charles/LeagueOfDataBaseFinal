using System.ComponentModel;

namespace LoDb.Api.Modules.PublicApi.Access;

/// <summary>
/// What a request of <c>/v1</c> needs to know of its key, as one read of the database left
/// it: the verdict, the rights, and the requests of the month.
/// </summary>
/// <remarks>
/// Sealed and marked immutable, so the cache hands out this instance instead of a copy.
/// </remarks>
/// <param name="Id">The row of <c>api_keys</c>.</param>
/// <param name="IsUsable">Active and not revoked.</param>
/// <param name="MonthlyQuota">Requests the plan pays for each month.</param>
/// <param name="CreditsBalance">Prepaid requests left when the key was read.</param>
/// <param name="RateLimitPerMin">Capacity of the token bucket, and its refill per minute.</param>
/// <param name="UsedThisMonth">
/// Requests <c>api_usage</c> counted for <paramref name="Month"/>.
/// </param>
/// <param name="Month">First day of the UTC month the key was read in.</param>
/// <param name="Sequence">Order of the read: a later read has a greater one.</param>
[ImmutableObject(true)]
internal sealed record ApiKeySnapshot(
    int Id,
    bool IsUsable,
    int MonthlyQuota,
    long CreditsBalance,
    int RateLimitPerMin,
    long UsedThisMonth,
    DateOnly Month,
    long Sequence);
