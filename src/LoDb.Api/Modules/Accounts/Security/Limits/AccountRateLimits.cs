using System.Threading.RateLimiting;
using LoDb.Api.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security.Limits;

/// <summary>
/// The rate limits of the accounts: the sign-ins of one client address, and the password
/// confirmations of one signed-in account.
/// </summary>
/// <remarks>
/// <para>
/// Added to the host's rate limiter by the accounts module; a refusal is the host's 429
/// <c>rate-limited</c>. Each bound slides over <see cref="SignInLimitOptions.AccountWindow"/>
/// in segments of a fifteenth of it, a minute by default.
/// </para>
/// <para>
/// An action of a signed-in account is limited by account, never by address: members
/// behind one NAT must not share a quota.
/// </para>
/// </remarks>
internal static class AccountRateLimits
{
    /// <summary>Sign-ins with a password or a Google code, by client address.</summary>
    public const string SignIn = "sign-in";

    /// <summary>Confirmations of an action by the account's password, by account.</summary>
    public const string PasswordConfirm = "password-confirm";

    public static void AddTo(RateLimiterOptions options, IOptions<SignInLimitOptions> limits)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(limits);
        var bounds = limits.Value;
        options.AddPolicy(SignIn, context => RateLimitPartition.GetSlidingWindowLimiter(
            RateLimitingPolicies.ClientKey(context.Connection.RemoteIpAddress),
            _ => AccountPartitions.Window(bounds.SignInPerWindow, bounds.AccountWindow)));
        options.AddPolicy(PasswordConfirm, context => AccountPartitions.OfAccount(
            context,
            bounds.PasswordConfirmsPerWindow,
            bounds.AccountWindow));
    }
}
