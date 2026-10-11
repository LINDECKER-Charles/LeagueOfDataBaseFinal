using LoDb.Api.Modules.Accounts.Security.Authenticator;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>
/// Identity's user manager, which also counts the lockouts in <see cref="SecurityMetrics"/>
/// and keeps the secrets of the second factor out of reach of a copy of the database.
/// </summary>
/// <remarks>
/// <para>
/// Every failure that counts toward the lockout comes through <see cref="AccessFailedAsync"/>,
/// whatever asked for the password or the code: a sign-in, a second factor, an erasure, the
/// enrolment of an authenticator. Counting here sees them all.
/// </para>
/// <para>
/// Identity's stores keep the authenticator key and the recovery codes in clear. Here the
/// key is sealed (<see cref="AuthenticatorKeyProtection"/>) and each code hashed
/// (<see cref="RecoveryCodeHashes"/>) before the store sees them; the store stays Identity's.
/// </para>
/// </remarks>
internal sealed class LoDbUserManager(
    IUserStore<User> store,
    IOptions<IdentityOptions> optionsAccessor,
    IPasswordHasher<User> passwordHasher,
    IEnumerable<IUserValidator<User>> userValidators,
    IEnumerable<IPasswordValidator<User>> passwordValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    ILogger<UserManager<User>> logger,
    SecurityMetrics metrics,
    AuthenticatorKeyProtection keys,
    RecoveryCodeHashes codes)
    : UserManager<User>(
        store,
        optionsAccessor,
        passwordHasher,
        userValidators,
        passwordValidators,
        keyNormalizer,
        errors,
        services,
        logger)
{
    private IUserAuthenticationTokenStore<User> Tokens =>
        Store as IUserAuthenticationTokenStore<User>
            ?? throw new NotSupportedException("The user store keeps no tokens.");

    public override async Task<IdentityResult> AccessFailedAsync(User user)
    {
        var wasLocked = await IsLockedOutAsync(user);
        var result = await base.AccessFailedAsync(user);
        if (result.Succeeded && !wasLocked && await IsLockedOutAsync(user))
        {
            metrics.RecordLockout(await IsInRoleAsync(user, Role.Admin));
        }

        return result;
    }

    public override async Task<string?> GetAuthenticatorKeyAsync(User user) =>
        keys.Reveal(await base.GetAuthenticatorKeyAsync(user));

    /// <remarks>As Identity's: a new key, then a new security stamp.</remarks>
    public override async Task<IdentityResult> ResetAuthenticatorKeyAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        await SetStoredAsync(
            user,
            IdentityTokenNames.AuthenticatorKey,
            keys.Seal(GenerateNewAuthenticatorKey()));
        return await UpdateSecurityStampAsync(user);
    }

    /// <returns>The codes in clear, shown once; the store keeps their hashes.</returns>
    public override async Task<IEnumerable<string>?> GenerateNewTwoFactorRecoveryCodesAsync(
        User user,
        int number)
    {
        ArgumentNullException.ThrowIfNull(user);
        var drawn = Enumerable.Range(0, number)
            .Select(_ => CreateTwoFactorRecoveryCode())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        await SetCodesAsync(user, drawn);
        return (await UpdateUserAsync(user)).Succeeded ? drawn : null;
    }

    /// <remarks>
    /// The codes left are written back hashed, those an earlier version left in clear
    /// included.
    /// </remarks>
    public override async Task<IdentityResult> RedeemTwoFactorRecoveryCodeAsync(
        User user,
        string code)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(code);
        var entries = await StoredCodesAsync(user);
        var redeemed = entries.FindIndex(entry => codes.Matches(entry, code));
        if (redeemed < 0)
        {
            return IdentityResult.Failed(ErrorDescriber.RecoveryCodeRedemptionFailed());
        }

        entries.RemoveAt(redeemed);
        await SetCodesAsync(user, entries);
        return await UpdateUserAsync(user);
    }

    /// <summary>
    /// Seals the key and hashes the codes an earlier version stored in clear. Run again, or
    /// on an account whose secrets are protected already, it rewrites the same values.
    /// </summary>
    public async Task<IdentityResult> ProtectStoredSecretsAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var key = await base.GetAuthenticatorKeyAsync(user);
        if (key is not null && !AuthenticatorKeyProtection.IsSealed(key))
        {
            await SetStoredAsync(user, IdentityTokenNames.AuthenticatorKey, keys.Seal(key));
        }

        await SetCodesAsync(user, await StoredCodesAsync(user));
        return await UpdateUserAsync(user);
    }

    private async Task<List<string>> StoredCodesAsync(User user)
    {
        var stored = await Tokens.GetTokenAsync(
            user,
            IdentityTokenNames.IdentityProvider,
            IdentityTokenNames.RecoveryCodes,
            CancellationToken);
        return [.. (stored ?? string.Empty).Split(
            IdentityTokenNames.CodeSeparator,
            StringSplitOptions.RemoveEmptyEntries)];
    }

    // Each entry hashed, unless it is already.
    private Task SetCodesAsync(User user, IEnumerable<string> entries) =>
        SetStoredAsync(
            user,
            IdentityTokenNames.RecoveryCodes,
            string.Join(IdentityTokenNames.CodeSeparator, entries.Select(codes.Protect)));

    private Task SetStoredAsync(User user, string name, string value) =>
        Tokens.SetTokenAsync(
            user,
            IdentityTokenNames.IdentityProvider,
            name,
            value,
            CancellationToken);
}
