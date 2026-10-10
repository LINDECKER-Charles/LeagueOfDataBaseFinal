using System.Threading.RateLimiting;
using LoDb.Api.Hosting;
using Microsoft.AspNetCore.RateLimiting;

namespace LoDb.Api.Modules.Catalog.WarmUp.Limits;

/// <summary>
/// The limit of the warm-ups one client starts: <c>LoDb:Catalog:WarmUp:PerClientPerHour</c>
/// from one address, or one IPv6 /64, over a sliding hour.
/// </summary>
/// <remarks>
/// Only the warm-up: a refusal leaves the visit going, the loader's watchdog moving on to the
/// page. Never the pickers, read at every opening of the build editor and the account's
/// selectors by members who may share an address (ADR 0009).
/// </remarks>
internal static class ColdWorkRateLimit
{
    public const string Policy = "cold-work";

    // One segment a minute, as the hourly policies of the host: a permit comes back an hour
    // after its use, to the minute.
    private const int SegmentsPerWindow = 60;

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public static void AddTo(RateLimiterOptions limiter, WarmUpOptions warmUp)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(warmUp);
        var permits = warmUp.PerClientPerHour;
        limiter.AddPolicy(Policy, context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                RateLimitingPolicies.ClientKey(context.Connection.RemoteIpAddress),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = Window,
                    SegmentsPerWindow = SegmentsPerWindow,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    }
}
