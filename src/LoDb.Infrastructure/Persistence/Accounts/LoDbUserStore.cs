using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Persistence.Accounts;

/// <summary>
/// Identity's EF store over <c>users</c>, reduced to what the schema maps: roles and tokens.
/// </summary>
/// <remarks>
/// Claims, external logins and passkeys have no table. Reads find nothing, so that the
/// claims factory and the sign-in manager work unchanged; writes are refused. Google stays on
/// <c>users.google_id</c>, which the legacy stack reads too.
/// </remarks>
internal sealed class LoDbUserStore(LoDbDbContext context, IdentityErrorDescriber? describer = null)
    : UserStore<User, Role, LoDbDbContext, int, IdentityUserClaim<int>, IdentityUserRole<int>,
        IdentityUserLogin<int>, IdentityUserToken<int>, IdentityRoleClaim<int>>(context, describer)
{
    // As many random bytes as the stamps UserManager writes.
    private const int StampBytes = 20;

    /// <summary>
    /// The stamp of the account, created and stored first when the legacy stack left it null.
    /// </summary>
    /// <remarks>
    /// <c>UserManager</c> throws on a null stamp, and every account the legacy stack writes
    /// has one: the next sign-in creates it instead. The first writer wins, so concurrent
    /// sign-ins of the same account end up with one stamp.
    /// </remarks>
    public override async Task<string?> GetSecurityStampAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var stamp = await base.GetSecurityStampAsync(user, cancellationToken);
        if (stamp is not null)
        {
            return stamp;
        }

        // The tracked entity takes the stored value; a later save writes the same one.
        user.SecurityStamp = await StoreMissingStampAsync(user.Id, cancellationToken);
        return user.SecurityStamp;
    }

    public override Task<IList<Claim>> GetClaimsAsync(
        User user,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IList<Claim>>([]);

    public override Task AddClaimsAsync(
        User user,
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default) =>
        throw Unmapped("claims");

    public override Task ReplaceClaimAsync(
        User user,
        Claim claim,
        Claim newClaim,
        CancellationToken cancellationToken = default) =>
        throw Unmapped("claims");

    public override Task RemoveClaimsAsync(
        User user,
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default) =>
        throw Unmapped("claims");

    public override Task<IList<User>> GetUsersForClaimAsync(
        Claim claim,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IList<User>>([]);

    public override Task AddLoginAsync(
        User user,
        UserLoginInfo login,
        CancellationToken cancellationToken = default) =>
        throw Unmapped("external logins");

    public override Task RemoveLoginAsync(
        User user,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken = default) =>
        throw Unmapped("external logins");

    public override Task<IList<UserLoginInfo>> GetLoginsAsync(
        User user,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IList<UserLoginInfo>>([]);

    public override Task AddOrUpdatePasskeyAsync(
        User user,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken) =>
        throw Unmapped("passkeys");

    public override Task<IList<UserPasskeyInfo>> GetPasskeysAsync(
        User user,
        CancellationToken cancellationToken) =>
        Task.FromResult<IList<UserPasskeyInfo>>([]);

    public override Task<User?> FindByPasskeyIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken) =>
        Task.FromResult<User?>(null);

    public override Task<UserPasskeyInfo?> FindPasskeyAsync(
        User user,
        byte[] credentialId,
        CancellationToken cancellationToken) =>
        Task.FromResult<UserPasskeyInfo?>(null);

    public override Task RemovePasskeyAsync(
        User user,
        byte[] credentialId,
        CancellationToken cancellationToken) =>
        throw Unmapped("passkeys");

    protected override Task<IdentityUserLogin<int>?> FindUserLoginAsync(
        int userId,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken) =>
        Task.FromResult<IdentityUserLogin<int>?>(null);

    protected override Task<IdentityUserLogin<int>?> FindUserLoginAsync(
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken) =>
        Task.FromResult<IdentityUserLogin<int>?>(null);

    private static NotSupportedException Unmapped(string what) =>
        new($"The LoDb schema maps no Identity {what}.");

    // An account not saved yet has no row: its stamp stays in memory until it is.
    private async Task<string> StoreMissingStampAsync(int userId, CancellationToken token)
    {
        var stamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(StampBytes));
        if (userId == default)
        {
            return stamp;
        }

        var written = await Context.Users
            .Where(row => row.Id == userId && row.SecurityStamp == null)
            .ExecuteUpdateAsync(row => row.SetProperty(user => user.SecurityStamp, stamp), token);
        if (written == 1)
        {
            return stamp;
        }

        var stored = await Context.Users.AsNoTracking()
            .Where(row => row.Id == userId)
            .Select(static row => row.SecurityStamp)
            .SingleOrDefaultAsync(token);
        return stored ?? stamp;
    }
}
