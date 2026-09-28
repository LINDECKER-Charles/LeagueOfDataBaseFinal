using System.Security.Claims;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Session;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>
/// The enrollment of an administrator's authenticator: a new key, then a code of it, which
/// turns the second factor on and opens the session with it.
/// </summary>
/// <remarks>
/// Only an account without a second factor enrols: one that has it signs in with it, so a
/// stolen password never replaces the authenticator of an administrator. A wrong code counts
/// toward the lockout, as at sign-in.
/// </remarks>
internal sealed class MfaEnrollment(
    UserManager<User> users,
    SignInManager<User> signIn,
    SessionReader sessions,
    AdminTrail trail,
    TimeProvider clock)
{
    public const string CodeField = "code";
    public const string InvalidCode = "invalid-code";

    // As many codes as the enrollment of Identity's own UI hands out.
    private const int RecoveryCodes = 10;

    // Authenticator apps show their codes in groups, such as "123 456" or "123-456".
    private const string CodeSeparators = " -";

    /// <summary>Draws a new key, replacing one drawn before and never confirmed.</summary>
    public async Task<(MfaSetup? Setup, AccountProblem? Problem)> StartAsync(
        ClaimsPrincipal principal)
    {
        if (await EnrollingAsync(principal) is not { } user)
        {
            return (null, await RefusalAsync(principal));
        }

        await users.ResetAuthenticatorKeyAsync(user);

        // The new key renewed the security stamp, which the session cookie must follow.
        await signIn.RefreshSignInAsync(user);
        var key = await users.GetAuthenticatorKeyAsync(user) ?? string.Empty;
        var setup = new MfaSetup
        {
            SharedKey = key,
            AuthenticatorUri = AuthenticatorUri.For(user.Email ?? user.UserName ?? "", key),
        };
        return (setup, null);
    }

    /// <summary>Turns the second factor on once <paramref name="code"/> matches the key.</summary>
    public async Task<(MfaConfirmation? Done, AccountProblem? Problem)> ConfirmAsync(
        HttpContext context,
        string? code)
    {
        if (await EnrollingAsync(context.User) is not { } user)
        {
            return (null, await RefusalAsync(context.User));
        }

        if (await CheckAsync(user, code) is { } refused)
        {
            return (null, refused);
        }

        await users.SetTwoFactorEnabledAsync(user, enabled: true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodes);
        await SignInWithSecondFactorAsync(context, user);
        trail.MfaEnrolled(user.Id);
        var done = new MfaConfirmation
        {
            RecoveryCodes = [.. codes ?? []],
            Session = await sessions.ReadAsync(context.User),
        };
        return (done, null);
    }

    private async Task<User?> EnrollingAsync(ClaimsPrincipal principal) =>
        await users.GetUserAsync(principal) is { TwoFactorEnabled: false } user ? user : null;

    private async Task<AccountProblem> RefusalAsync(ClaimsPrincipal principal) =>
        await users.GetUserAsync(principal) is null
            ? AccountProblem.AuthenticationRequired()
            : AdminProblems.Conflict(AdminProblems.MfaAlreadyEnrolled);

    private async Task<AccountProblem?> CheckAsync(User user, string? code)
    {
        var digits = Digits(code);
        if (digits is null)
        {
            return AdminProblems.Invalid(CodeField, FieldErrors.Required);
        }

        if (await users.IsLockedOutAsync(user))
        {
            return AccountProblem.AccountLocked(LockoutLeft(user));
        }

        var provider = users.Options.Tokens.AuthenticatorTokenProvider;
        if (await users.VerifyTwoFactorTokenAsync(user, provider, digits))
        {
            await users.ResetAccessFailedCountAsync(user);
            return null;
        }

        await users.AccessFailedAsync(user);
        return AdminProblems.Invalid(CodeField, InvalidCode);
    }

    // As a sign-in with the code would: the same properties, "remember me" included.
    private async Task SignInWithSecondFactorAsync(HttpContext context, User user)
    {
        var current = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        Claim[] methods = [new(AuthenticationMethods.ClaimType, AuthenticationMethods.MultiFactor)];
        await signIn.SignInWithClaimsAsync(user, current.Properties, methods);
    }

    private TimeSpan LockoutLeft(User user) =>
        user.LockoutEnd is { } end && end > clock.GetUtcNow()
            ? end - clock.GetUtcNow()
            : TimeSpan.Zero;

    private static string? Digits(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : string.Concat(code.Where(static character => !CodeSeparators.Contains(character)));
}
