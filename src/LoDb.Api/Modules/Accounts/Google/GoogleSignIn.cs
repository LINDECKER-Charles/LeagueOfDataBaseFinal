using System.Security.Claims;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Google;

/// <summary>
/// Signs in the account of a Google profile, provisioned first: to the session cookie at the
/// end of the web flow, or to the tokens of an app's exchange.
/// </summary>
/// <remarks>
/// Refused as a password sign-in would be: a banned or locked account, and an account with a
/// second factor, which Google cannot stand for; it signs in with its password and its code.
/// </remarks>
internal sealed class GoogleSignIn(
    GoogleProvisioner provisioner,
    SignInManager<User> signIn,
    AccountAudit audit)
{
    /// <summary>Signs in to the session cookie.</summary>
    /// <returns>Null on success, else one of <see cref="GoogleFailures"/>.</returns>
    public Task<string?> SignInWebAsync(
        GoogleProfile profile,
        bool isPersistent,
        CancellationToken cancellationToken) =>
        SignInAsync(new Attempt(profile, SignInChannel.Session, isPersistent), cancellationToken);

    /// <summary>Signs in to the tokens, which the bearer handler writes in the response.</summary>
    /// <returns>Null on success, else one of <see cref="GoogleFailures"/>.</returns>
    public Task<string?> SignInAppAsync(
        GoogleProfile profile,
        CancellationToken cancellationToken) =>
        SignInAsync(new Attempt(profile, SignInChannel.Token, false), cancellationToken);

    private async Task<string?> SignInAsync(Attempt attempt, CancellationToken cancellationToken)
    {
        var provisioned = await provisioner.ProvisionAsync(attempt.Profile, cancellationToken);
        var account = provisioned.Account;
        var failure = account is null
            ? provisioned.Failure ?? GoogleFailures.Failed
            : await RefusalAsync(account);
        if (failure is not null)
        {
            await audit.SignInFailedAsync(attempt.Profile.Email, failure, cancellationToken);
            return failure;
        }

        signIn.AuthenticationScheme = attempt.Channel.Scheme;
        Claim[] methods =
        [
            new(ClaimTypes.AuthenticationMethod, GoogleDefaults.AuthenticationScheme),
        ];
        await signIn.SignInWithClaimsAsync(account!, attempt.IsPersistent, methods);
        await audit.SignedInAsync(AccountAudit.GoogleMethod, attempt.Channel, cancellationToken);
        return null;
    }

    private async Task<string?> RefusalAsync(User account)
    {
        if (account.IsBanned)
        {
            return GoogleFailures.AccountBanned;
        }

        if (await signIn.UserManager.IsLockedOutAsync(account))
        {
            return GoogleFailures.AccountLocked;
        }

        return account.TwoFactorEnabled ? GoogleFailures.TwoFactorRequired : null;
    }

    private sealed record Attempt(GoogleProfile Profile, SignInChannel Channel, bool IsPersistent);
}
