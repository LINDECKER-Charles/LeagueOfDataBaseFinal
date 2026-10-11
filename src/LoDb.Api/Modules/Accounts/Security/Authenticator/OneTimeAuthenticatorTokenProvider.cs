using System.Globalization;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Security.Authenticator;

/// <summary>
/// The authenticator codes of Identity, each one accepted once: a code seen, at sign-in or
/// at the enrolment, cannot be played again by whoever looked over a shoulder or read it on
/// the way.
/// </summary>
/// <remarks>
/// <para>
/// The step of the last code accepted is kept with the account
/// (<see cref="IdentityTokenNames.AuthenticatorStep"/>): a code counts only for a later
/// step. It is saved with the account's concurrency stamp, so of two requests carrying the
/// same code at once, one saves and the other is refused.
/// </para>
/// <para>
/// One step of drift either side, against two in Identity's provider: a phone whose clock
/// is a little off still signs in, and a code lives 90 seconds at most.
/// </para>
/// </remarks>
internal sealed class OneTimeAuthenticatorTokenProvider(TimeProvider clock)
    : IUserTwoFactorTokenProvider<User>
{
    private const int Drift = 1;

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return !string.IsNullOrWhiteSpace(await manager.GetAuthenticatorKeyAsync(user));
    }

    /// <summary>The app makes the codes: the server never sends one.</summary>
    public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user) =>
        Task.FromResult(string.Empty);

    public async Task<bool> ValidateAsync(
        string purpose,
        string token,
        UserManager<User> manager,
        User user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        var key = AuthenticatorCodes.KeyOf(await manager.GetAuthenticatorKeyAsync(user));
        if (key is null
            || !int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            || MatchingStep(key, code, await LastStepAsync(manager, user)) is not { } step)
        {
            return false;
        }

        var saved = await manager.SetAuthenticationTokenAsync(
            user,
            IdentityTokenNames.LoDbProvider,
            IdentityTokenNames.AuthenticatorStep,
            step.ToString(CultureInfo.InvariantCulture));
        return saved.Succeeded;
    }

    // The earliest step of the window the code matches, past the last one accepted.
    private long? MatchingStep(byte[] key, int code, long? last)
    {
        var now = AuthenticatorCodes.StepAt(clock.GetUtcNow());
        for (var step = now - Drift; step <= now + Drift; step++)
        {
            if (step > last.GetValueOrDefault(long.MinValue)
                && AuthenticatorCodes.CodeOf(key, step) == code)
            {
                return step;
            }
        }

        return null;
    }

    private static async Task<long?> LastStepAsync(UserManager<User> manager, User user)
    {
        var stored = await manager.GetAuthenticationTokenAsync(
            user,
            IdentityTokenNames.LoDbProvider,
            IdentityTokenNames.AuthenticatorStep);
        return long.TryParse(
            stored,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var step) ? step : null;
    }
}
