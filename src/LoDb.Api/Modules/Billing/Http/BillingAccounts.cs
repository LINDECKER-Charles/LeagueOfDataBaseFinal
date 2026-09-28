using System.Security.Claims;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Billing.Http;

/// <summary>The account a payment call acts for: the signed-in one, as stored now.</summary>
internal sealed class BillingAccounts(UserManager<User> users)
{
    /// <summary>
    /// The account of <paramref name="principal"/>; null for a visitor, and for an account
    /// gone or banned, which a session not revalidated yet may still name.
    /// </summary>
    public async Task<User?> FindAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        return user is { IsBanned: false } ? user : null;
    }
}
