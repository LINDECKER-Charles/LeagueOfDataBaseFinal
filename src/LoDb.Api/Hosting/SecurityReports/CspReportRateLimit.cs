using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// The reports one client address may send in a minute
/// (<see cref="SecurityReportsOptions.PerClientPerMinute"/>). An anonymous diagnostic, not an
/// action of an account: a client past its share only loses reports.
/// </summary>
internal static class CspReportRateLimit
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static void AddTo(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(SecurityReportsRoutes.PerClientPolicy, static context =>
        {
            var permits = context.RequestServices
                .GetRequiredService<IOptions<SecurityReportsOptions>>().Value.PerClientPerMinute;
            return RateLimitPartition.GetFixedWindowLimiter(
                RateLimitingPolicies.ClientKey(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = Window,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
        });
    }
}
