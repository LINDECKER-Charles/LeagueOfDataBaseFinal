using System.Globalization;
using System.Linq.Expressions;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Accounts.Security.Authenticator;

/// <summary>
/// Protects the second-factor secrets an earlier version stored in clear: the authenticator
/// keys get sealed and the recovery codes hashed, account by account.
/// </summary>
/// <remarks>
/// <para>
/// Safe at any time and as often as wished: an account whose secrets are protected is not
/// even read again, and the readers accept both forms, so an administrator keeps signing in
/// with the same app and the same codes before, during and after. Each account is saved
/// with its concurrency stamp, so a sign-in that redeems a code meanwhile wins, and the
/// account is tried again on the next run.
/// </para>
/// <para>
/// Run by <c>AuthenticatorSecretsJob</c>, which needs the keys of Data Protection: the
/// <c>migrate</c> container does not hold them.
/// </para>
/// </remarks>
internal sealed class AuthenticatorSecretsUpkeep(LoDbDbContext context, LoDbUserManager users)
{
    // Neither prefix holds a wildcard of LIKE.
    private const string SealedPattern = AuthenticatorKeyProtection.SealedPrefix + "%";
    private const string HashedPattern = "$argon2%";

    // What an earlier version left in clear: a key not sealed, codes not hashed.
    private static readonly Expression<Func<IdentityUserToken<int>, bool>> InClear =
        static token => token.LoginProvider == IdentityTokenNames.IdentityProvider
            && token.Value != null
            && ((token.Name == IdentityTokenNames.AuthenticatorKey
                    && !EF.Functions.Like(token.Value, SealedPattern))
                || (token.Name == IdentityTokenNames.RecoveryCodes
                    && token.Value != string.Empty
                    && !EF.Functions.Like(token.Value, HashedPattern)));

    /// <returns>How many accounts had secrets in clear and have them protected now.</returns>
    public async Task<int> ProtectAsync(CancellationToken cancellationToken)
    {
        var accounts = await context.Set<IdentityUserToken<int>>()
            .AsNoTracking()
            .Where(InClear)
            .Select(static token => token.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var protectedCount = 0;
        foreach (var id in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            protectedCount += await ProtectAccountAsync(id) ? 1 : 0;
        }

        return protectedCount;
    }

    private async Task<bool> ProtectAccountAsync(int id) =>
        await users.FindByIdAsync(id.ToString(CultureInfo.InvariantCulture)) is { } user
        && (await users.ProtectStoredSecretsAsync(user)).Succeeded;
}
