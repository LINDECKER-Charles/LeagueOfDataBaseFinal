using System.Security.Claims;
using System.Threading.RateLimiting;

namespace LoDb.Api.Modules.Accounts.Security.Limits;

/// <summary>
/// The sliding windows of the accounts' rate limits, and the partition of an action of a
/// signed-in account: one per account, never one per address.
/// </summary>
/// <remarks>
/// Members behind one NAT must not share a quota (ADR 0009): an action that needs a session
/// is counted by the account's <see cref="ClaimTypes.NameIdentifier"/>. Authorization runs
/// before the rate limiter and refuses an anonymous call, which has nothing to count.
/// </remarks>
internal static class AccountPartitions
{
    // A permit comes back within a fifteenth of the window: a minute for a quarter of an
    // hour, four for an hour.
    private const int SegmentsPerWindow = 15;

    /// <summary>
    /// The partition of <paramref name="context"/>'s account: <paramref name="permits"/>
    /// requests per sliding <paramref name="window"/>.
    /// </summary>
    public static RateLimitPartition<string> OfAccount(
        HttpContext context,
        int permits,
        TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } account
            ? RateLimitPartition.GetSlidingWindowLimiter(account, _ => Window(permits, window))
            : RateLimitPartition.GetNoLimiter(string.Empty);
    }

    /// <summary>A sliding window that refuses at once, without a queue.</summary>
    public static SlidingWindowRateLimiterOptions Window(int permits, TimeSpan window) => new()
    {
        PermitLimit = permits,
        Window = window,
        SegmentsPerWindow = SegmentsPerWindow,
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
