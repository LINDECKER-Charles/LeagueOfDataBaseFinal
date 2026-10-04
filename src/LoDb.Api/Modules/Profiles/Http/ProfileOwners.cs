using System.Security.Claims;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>The account a profile call acts on: the signed-in one, as stored now.</summary>
internal sealed class ProfileOwners(UserManager<User> users)
{
    /// <summary>
    /// The account of <paramref name="principal"/>, tracked for an update; null when it is
    /// gone or banned, which a session not revalidated yet may still name.
    /// </summary>
    public async Task<User?> FindAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        return user is { IsBanned: false } ? user : null;
    }
}
