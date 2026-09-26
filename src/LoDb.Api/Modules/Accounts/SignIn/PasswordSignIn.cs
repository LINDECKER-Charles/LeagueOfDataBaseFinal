using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// A sign-in with a password, and with the second factor of the accounts that have one, to
/// the session cookie of the web or to the tokens of the apps.
/// </summary>
/// <remarks>
/// Every failure counts toward the lockout, a wrong second factor included. A banned account
/// is refused once its password is right, so that whoever does not know the password does
/// not learn of the ban.
/// </remarks>
internal sealed class PasswordSignIn(
    AccountLookup lookup,
    SignInManager<User> signIn,
    AccountAudit audit,
    TimeProvider clock)
{
    // Authenticator apps show their codes in groups, such as "123 456" or "123-456".
    private const string CodeSeparators = " -";

    /// <summary>
    /// Signs in with <paramref name="credentials"/> through <paramref name="channel"/>, or
    /// tells why not; on success, the sign-in manager has written the cookie or the tokens.
    /// </summary>
    public async Task<AccountProblem?> SignInAsync(
        PasswordCredentials credentials,
        SignInChannel channel,
        CancellationToken cancellationToken)
    {
        var errors = new FieldErrors();
        errors.RequireText(AccountFields.Identifier, credentials.Identifier);
        errors.RequireText(AccountFields.Password, credentials.Password);
        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        signIn.AuthenticationScheme = channel.Scheme;
        var user = await lookup.FindAsync(credentials.Identifier!);
        var problem = user is null
            ? AccountProblem.InvalidCredentials()
            : await CheckAsync(user, credentials);
        if (problem is null)
        {
            await audit.SignedInAsync(AccountAudit.PasswordMethod, channel, cancellationToken);
        }
        else if (problem != AccountProblem.TwoFactorRequired())
        {
            // Asking for the second factor is a step of the sign-in, not a failure.
            await audit.SignInFailedAsync(credentials.Identifier, problem.Code, cancellationToken);
        }

        return problem;
    }

    private async Task<AccountProblem?> CheckAsync(User user, PasswordCredentials credentials)
    {
        var result = await signIn.PasswordSignInAsync(
            user,
            credentials.Password!,
            credentials.RememberMe,
            lockoutOnFailure: true);
        if (!result.RequiresTwoFactor)
        {
            return Refusal(user, result, AccountProblem.InvalidCredentials);
        }

        return await SecondFactorAsync(credentials) is { } second
            ? Refusal(user, second, AccountProblem.InvalidTwoFactorCode)
            : AccountProblem.TwoFactorRequired();
    }

    // Only within the request whose password check asked for it: no cookie keeps the account
    // between two requests, so the client sends the password again with the code.
    private async Task<SignInResult?> SecondFactorAsync(PasswordCredentials credentials)
    {
        if (AuthenticatorCode(credentials.TwoFactorCode) is { } code)
        {
            return await signIn.TwoFactorAuthenticatorSignInAsync(
                code,
                credentials.RememberMe,
                rememberClient: false);
        }

        return RecoveryCode(credentials.RecoveryCode) is { } recovery
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(recovery)
            : null;
    }

    private AccountProblem? Refusal(User user, SignInResult result, Func<AccountProblem> wrong)
    {
        if (result.Succeeded)
        {
            return null;
        }

        if (result.IsLockedOut)
        {
            return AccountProblem.AccountLocked(LockoutLeft(user));
        }

        // The confirmation options are off: only the ban refuses an account this way.
        return result.IsNotAllowed ? AccountProblem.AccountBanned() : wrong();
    }

    // Identity dates the lockout itself; the delay stays within one lockout.
    private TimeSpan LockoutLeft(User user)
    {
        var longest = AccountsSecurityRegistration.LockoutDuration;
        return user.LockoutEnd is { } end
            ? TimeSpan.FromTicks(Math.Clamp((end - clock.GetUtcNow()).Ticks, 0, longest.Ticks))
            : longest;
    }

    private static string? AuthenticatorCode(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : string.Concat(code.Where(static character => !CodeSeparators.Contains(character)));

    // Identity writes recovery codes in capitals, such as "AB3CD-EF4GH".
    private static string? RecoveryCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
