using System.Security.Claims;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Session;

/// <summary>
/// Describes the session of a principal from the account row, as stored now: a verification
/// made in another tab shows at once.
/// </summary>
internal sealed class SessionReader(UserManager<User> users)
{
    /// <summary>
    /// The session of <paramref name="principal"/>: anonymous when it names no account, or a
    /// banned one whose session is not revalidated yet.
    /// </summary>
    public async Task<AccountSession> ReadAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return AccountSession.Anonymous;
        }

        var user = await users.GetUserAsync(principal);
        return user is null or { IsBanned: true }
            ? AccountSession.Anonymous
            : new AccountSession { User = await DescribeAsync(user, principal) };
    }

    private async Task<AccountUser> DescribeAsync(User user, ClaimsPrincipal principal) => new()
    {
        Id = user.Id,
        Username = user.UserName ?? string.Empty,
        Email = user.Email ?? string.Empty,
        RiotTagline = user.RiotTagline,
        EmailVerified = user.EmailConfirmed,
        HasPassword = user.PasswordHash is not null,
        IsSupporter = user.IsSupporter,
        TwoFactorEnabled = user.TwoFactorEnabled,
        MultiFactor = AuthenticationMethods.IsMultiFactor(principal),
        Roles = [.. await users.GetRolesAsync(user)],
    };
}
