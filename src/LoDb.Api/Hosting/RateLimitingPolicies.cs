using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LoDb.Api.Hosting;

/// <summary>
/// Named rate limiting policies of the API.
/// </summary>
/// <remarks>
/// <para>
/// The rate limiter is wired into the host from the start (services and middleware, after
/// authorization), so the security chantier only declares its policies here. A module may
/// also add its own through <c>services.Configure&lt;RateLimiterOptions&gt;</c>.
/// </para>
/// <para>
/// Each policy counts the requests of one client address over a sliding hour, as the legacy
/// stack does. The address is the one the forwarded headers resolved, so the real client's
/// behind the trusted proxies. The limits live in the memory of each instance.
/// </para>
/// </remarks>
internal static class RateLimitingPolicies
{
    /// <summary>Account registrations: 5 an hour.</summary>
    public const string Registration = "registration";

    /// <summary>Password reset e-mails asked for: 5 an hour, whatever the account.</summary>
    public const string PasswordReset = "password-reset";

    /// <summary>Contact messages: 5 an hour.</summary>
    public const string Contact = "contact";

    /// <summary>Donation checkouts opened: 10 an hour.</summary>
    public const string DonationCheckout = "donation-checkout";

    private const int RegistrationsPerHour = 5;
    private const int PasswordResetsPerHour = 5;
    private const int ContactsPerHour = 5;
    private const int DonationCheckoutsPerHour = 10;

    // One segment a minute: a permit comes back an hour after its use, to the minute.
    private const int SegmentsPerWindow = 60;

    private const string CodeExtension = "code";
    private const string RejectedCode = "rate-limited";
    private const string RejectedTitle = "Too many requests from this address: try again later.";
    private const string UnknownClient = "unknown";

    // A subscriber usually gets a whole /64: counting single addresses would let one
    // client rotate through billions of them.
    private const int Ipv6PrefixBytes = 8;
    private const string Ipv6PrefixSuffix = "/64";

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public static IServiceCollection AddLoDbRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddRateLimiter(static options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RejectAsync;
            AddHourly(options, Registration, RegistrationsPerHour);
            AddHourly(options, PasswordReset, PasswordResetsPerHour);
            AddHourly(options, Contact, ContactsPerHour);
            AddHourly(options, DonationCheckout, DonationCheckoutsPerHour);
        });

    /// <summary>
    /// The key a client's requests are counted under: its address, or its IPv6 /64.
    /// </summary>
    public static string ClientKey(IPAddress? address)
    {
        if (address is null)
        {
            return UnknownClient;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, Ipv6PrefixBytes, bytes.Length - Ipv6PrefixBytes);
        return new IPAddress(bytes) + Ipv6PrefixSuffix;
    }

    private static void AddHourly(RateLimiterOptions options, string policy, int permits) =>
        options.AddPolicy(policy, context => RateLimitPartition.GetSlidingWindowLimiter(
            ClientKey(context.Connection.RemoteIpAddress),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = Window,
                SegmentsPerWindow = SegmentsPerWindow,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    // A sliding window may not tell when a permit comes back: the next segment is the
    // earliest it can.
    private static ValueTask RejectAsync(OnRejectedContext context, CancellationToken token)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
            ? delay
            : Window / SegmentsPerWindow;
        var http = context.HttpContext;
        http.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
            .ToString(CultureInfo.InvariantCulture);
        var problem = TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: RejectedTitle,
            extensions: new Dictionary<string, object?> { [CodeExtension] = RejectedCode });
        return new ValueTask(problem.ExecuteAsync(http));
    }
}
