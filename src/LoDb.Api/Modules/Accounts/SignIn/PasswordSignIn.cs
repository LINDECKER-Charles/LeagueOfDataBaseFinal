using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Security.Admission;
using LoDb.Api.Modules.Accounts.Security.Limits;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// A sign-in with a password, and with the second factor of the accounts that have one, to
/// the session cookie of the web or to the tokens of the apps.
/// </summary>
/// <remarks>
/// <para>
/// Every failure counts toward the lockout, a wrong second factor included. A banned account
/// is refused once its password is right, so that whoever does not know the password does
/// not learn of the ban.
/// </para>
/// <para>
/// The <see cref="LoginThrottle"/> is consulted before the account is looked up, and again
/// under the permit of the hashing admission, which also covers the second factor and the
/// count of the failure: attempts sent together from one address cannot all get past it.
/// A throttled attempt is neither looked up nor journaled, only counted.
/// </para>
/// </remarks>
internal sealed class PasswordSignIn(
    AccountLookup lookup,
    SignInManager<User> signIn,
    HashingAdmission admission,
    LoginThrottle throttle,
    SecurityMetrics metrics,
    AccountAudit audit,
    TimeProvider clock)
{
    // Authenticator apps show their codes in groups, such as "123 456" or "123-456".
    private const string CodeSeparators = " -";

    // The result each refusal is counted under; a success is counted apart.
    private static readonly Dictionary<string, string> Results = new(StringComparer.Ordinal)
    {
        [AccountProblem.InvalidCredentials().Code] = SecurityMetrics.Failure,
        [AccountProblem.InvalidTwoFactorCode().Code] = SecurityMetrics.Failure,
        [AccountProblem.TwoFactorRequired().Code] = SecurityMetrics.TwoFactorRequired,
        [AccountProblem.AccountLocked(TimeSpan.Zero).Code] = SecurityMetrics.LockedOut,
        [AccountProblem.AccountBanned().Code] = SecurityMetrics.Banned,
    };

    /// <summary>
    /// Signs in with <paramref name="credentials"/> through <paramref name="channel"/>, or
    /// tells why not; on success, the sign-in manager has written the cookie or the tokens.
    /// </summary>
    /// <exception cref="HashingBusyException">No hashing permit came in time.</exception>
    public async Task<AccountProblem?> SignInAsync(
        PasswordCredentials credentials,
        SignInChannel channel,
        CancellationToken cancellationToken)
    {
        if (Invalid(credentials) is { } invalid)
        {
            return invalid;
        }

        signIn.AuthenticationScheme = channel.Scheme;
        var client = signIn.Context.Connection.RemoteIpAddress;
        var attempt = new Attempt(
            credentials,
            channel,
            LoginThrottle.KeyOf(credentials.Identifier!, client));
        if (throttle.IsThrottled(attempt.Key))
        {
            return Throttled(attempt, SecurityMetrics.UncheckedAccount);
        }

        var user = await lookup.FindAsync(credentials.Identifier!);
        var problem = user is null
            ? Counted(attempt, SecurityMetrics.NoAccount, AccountProblem.InvalidCredentials())
            : await CheckAsync(user, attempt, cancellationToken);
        await AuditAsync(attempt, problem, cancellationToken);
        return problem;
    }

    // Checked before anything else: a value past its bound is neither looked up nor counted.
    private static AccountProblem? Invalid(PasswordCredentials credentials)
    {
        var errors = new FieldErrors();
        errors.RequireShape(
            AccountFields.Identifier,
            credentials.Identifier,
            SignInFieldBounds.IsIdentifier);
        errors.RequireText(AccountFields.Password, credentials.Password);
        errors.CheckShape(
            AccountFields.TwoFactorCode,
            credentials.TwoFactorCode,
            SignInFieldBounds.IsAuthenticatorCode);
        errors.CheckShape(
            AccountFields.RecoveryCode,
            credentials.RecoveryCode,
            SignInFieldBounds.IsRecoveryCode);
        return errors.IsEmpty ? null : errors.ToProblem();
    }

    private async Task<AccountProblem?> CheckAsync(
        User user,
        Attempt attempt,
        CancellationToken cancellationToken)
    {
        var account = await signIn.UserManager.IsInRoleAsync(user, Role.Admin)
            ? SecurityMetrics.AdminAccount
            : SecurityMetrics.MemberAccount;
        using var permit = await admission.EnterAsync(cancellationToken);
        if (throttle.IsThrottled(attempt.Key))
        {
            return Throttled(attempt, account);
        }

        var problem = await VerifyAsync(user, attempt.Credentials);
        return Counted(attempt, account, problem);
    }

    // Counts the attempt, and toward the throttle a refusal that also counts toward the
    // lockout: a wrong password or code, or an attempt on a locked account.
    private AccountProblem? Counted(Attempt attempt, string account, AccountProblem? problem)
    {
        var result = problem is null
            ? SecurityMetrics.Success
            : Results.GetValueOrDefault(problem.Code, SecurityMetrics.Failure);
        metrics.RecordSignIn(result, account, attempt.Channel.AuditName);
        if (result is SecurityMetrics.Failure or SecurityMetrics.LockedOut)
        {
            throttle.RecordFailure(attempt.Key);
        }

        return problem;
    }

    private AccountProblem Throttled(Attempt attempt, string account)
    {
        metrics.RecordSignIn(SecurityMetrics.Throttled, account, attempt.Channel.AuditName);
        return AccountProblem.SignInThrottled(throttle.RetryAfter);
    }

    private async Task<AccountProblem?> VerifyAsync(User user, PasswordCredentials credentials)
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
            return AccountProblem.AccountLocked(LockoutTime.Left(user, clock));
        }

        // The confirmation options are off: only the ban refuses an account this way.
        return result.IsNotAllowed ? AccountProblem.AccountBanned() : wrong();
    }

    private async Task AuditAsync(
        Attempt attempt,
        AccountProblem? problem,
        CancellationToken cancellationToken)
    {
        if (problem is null)
        {
            await audit.SignedInAsync(
                AccountAudit.PasswordMethod,
                attempt.Channel,
                cancellationToken);
        }
        else if (problem != AccountProblem.TwoFactorRequired())
        {
            // Asking for the second factor is a step of the sign-in, not a failure.
            await audit.SignInFailedAsync(
                attempt.Credentials.Identifier,
                problem.Code,
                cancellationToken);
        }
    }


    private static string? AuthenticatorCode(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : string.Concat(code.Where(static character => !CodeSeparators.Contains(character)));

    // Identity writes recovery codes in capitals, such as "AB3CD-EF4GH".
    private static string? RecoveryCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    /// <summary>One sign-in: what was sent, where it lands, and its throttle key.</summary>
    private sealed record Attempt(
        PasswordCredentials Credentials,
        SignInChannel Channel,
        string Key);
}
