using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// The claims of a sign-in once <see cref="AccountsOptions.WaiveSecondFactor"/> marks it as
/// made with a second factor, which the options allow in Development only.
/// </summary>
/// <remarks>
/// The mark goes into the session itself, not each request's principal: the <c>Admin</c>
/// policy, its <c>mfa-required</c> refusal and the session the front reads then agree.
/// </remarks>
internal sealed class SecondFactorWaiver(IOptions<AccountsOptions> options)
{
    /// <summary>
    /// <paramref name="claims"/>, with the second factor among the sign-in methods when the
    /// waiver is on and it is not there yet.
    /// </summary>
    public IReadOnlyList<Claim> Complete(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        List<Claim> completed = [.. claims];
        if (options.Value.WaiveSecondFactor && !completed.Exists(IsMultiFactor))
        {
            completed.Add(
                new Claim(AuthenticationMethods.ClaimType, AuthenticationMethods.MultiFactor));
        }

        return completed;
    }

    private static bool IsMultiFactor(Claim claim) =>
        claim is
        {
            Type: AuthenticationMethods.ClaimType,
            Value: AuthenticationMethods.MultiFactor,
        };
}
