using System.Threading.RateLimiting;
using LoDb.Api.Hosting;
using Microsoft.AspNetCore.RateLimiting;

namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>
/// The beacon's limit: 120 views a minute from one address, far above any reader's pace, so
/// that a script cannot flood the queue from a single client.
/// </summary>
internal static class BeaconRateLimit
{
    public const int ViewsPerMinute = 120;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static void AddTo(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(CaptureRoutes.BeaconPolicy, static context =>
            RateLimitPartition.GetFixedWindowLimiter(
                RateLimitingPolicies.ClientKey(context.Connection.RemoteIpAddress),
                static _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = ViewsPerMinute,
                    Window = Window,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    }
}
