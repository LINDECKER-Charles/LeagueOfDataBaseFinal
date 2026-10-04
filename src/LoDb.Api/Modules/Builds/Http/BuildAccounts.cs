using System.Globalization;
using System.Security.Claims;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Builds.Http;

/// <summary>The account a build call acts for: the signed-in one, as stored now.</summary>
internal sealed class BuildAccounts(UserManager<User> users)
{
    /// <summary>
    /// The account of <paramref name="principal"/>; null when it is gone or banned, which a
    /// session not revalidated yet may still name.
    /// </summary>
    public async Task<User?> FindAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        return user is { IsBanned: false } ? user : null;
    }

    /// <summary>
    /// The id of the signed-in account, read from the session alone; null for a visitor. It
    /// only picks the caller's own votes out of a page.
    /// </summary>
    public int? IdOf(ClaimsPrincipal principal) =>
        int.TryParse(
            users.GetUserId(principal),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var id)
            ? id
            : null;
}
