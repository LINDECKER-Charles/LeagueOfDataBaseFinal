using System.Threading.RateLimiting;
using LoDb.Api.Hosting;
using Microsoft.AspNetCore.RateLimiting;

namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>
/// The limit of the reset link check: 30 an hour from one address, far above the reloads of
/// a reader, counted apart so that opening a link never uses up the requests for a new one
/// (<see cref="RateLimitingPolicies.PasswordReset"/>).
/// </summary>
internal static class ResetCheckRateLimit
{
    public const string Policy = "password-reset-check";
    public const int ChecksPerHour = 30;

    // One segment a minute, as the hourly policies of the host: a permit comes back an hour
    // after its use, to the minute.
    private const int SegmentsPerWindow = 60;

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public static void AddTo(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(Policy, static context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                RateLimitingPolicies.ClientKey(context.Connection.RemoteIpAddress),
                static _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = ChecksPerHour,
                    Window = Window,
                    SegmentsPerWindow = SegmentsPerWindow,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    }
}
