using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using LoDb.Api.Hosting;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security.Limits;

/// <summary>
/// Counts the failed sign-ins of each pair of an identifier and a client address, and of each
/// pair of an account and a client address, over a sliding window, and throttles a pair once
/// it reaches its bound.
/// </summary>
/// <remarks>
/// <para>
/// The identifier's pair is consulted before the account is looked up, and answered the same
/// whether an account bears the identifier or not. The account's pair joins it once the
/// account is found: its e-mail and its username, in any spelling the lookup accepts, name
/// one account and share its failures. Only failures count: a member who signs in, out and in
/// again is never throttled. The bound stays under the lockout threshold, so that one address
/// alone cannot lock an account within one window; many addresses still can, the compromise
/// the lockout keeps. The lockout counts failures in a row, not per window: this bound slows
/// one address down, it does not make the lockout forget.
/// </para>
/// <para>
/// In the memory of the instance, as the host's rate limits: a pair left alone for a window
/// is forgotten.
/// </para>
/// </remarks>
internal sealed class LoginThrottle : IDisposable
{
    private const int SegmentsPerWindow = 15;
    // An address never holds a line break: the last one splits the pair unambiguously.
    private const string Separator = "\n";
    // A trimmed identifier never starts with a line break: no identifier's key is an
    // account's.
    private const string AccountMark = "\n";
    private const int OneFailure = 1;

    private readonly PartitionedRateLimiter<string> _failures;

    public LoginThrottle(IOptions<SignInLimitOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var limits = options.Value;
        RetryAfter = limits.AccountWindow / SegmentsPerWindow;
        _failures = PartitionedRateLimiter.Create<string, string>(
            key => RateLimitPartition.GetSlidingWindowLimiter(
                key,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = limits.SignInFailuresPerIdentifier,
                    Window = limits.AccountWindow,
                    SegmentsPerWindow = SegmentsPerWindow,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }),
            StringComparer.Ordinal);
    }

    /// <summary>The earliest a throttled pair may get an attempt back: one segment.</summary>
    public TimeSpan RetryAfter { get; }

    /// <summary>
    /// The pair an attempt is counted under: the identifier as Identity normalizes it, and
    /// the address or IPv6 /64 of the host's rate limits.
    /// </summary>
    public static string KeyOf(string identifier, IPAddress? address)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return string.Concat(
            identifier.Trim().ToUpperInvariant(),
            Separator,
            RateLimitingPolicies.ClientKey(address));
    }

    /// <summary>
    /// The pair the failures on the account <paramref name="accountId"/> are also counted
    /// under, whatever identifier named it, with the address of <see cref="KeyOf"/>.
    /// </summary>
    public static string AccountKeyOf(int accountId, IPAddress? address) =>
        string.Concat(
            AccountMark,
            accountId.ToString(CultureInfo.InvariantCulture),
            Separator,
            RateLimitingPolicies.ClientKey(address));

    public bool IsThrottled(string key) =>
        _failures.GetStatistics(key) is { CurrentAvailablePermits: <= 0 };

    public void RecordFailure(string key)
    {
        using var lease = _failures.AttemptAcquire(key, OneFailure);
    }

    public void Dispose() => _failures.Dispose();
}
