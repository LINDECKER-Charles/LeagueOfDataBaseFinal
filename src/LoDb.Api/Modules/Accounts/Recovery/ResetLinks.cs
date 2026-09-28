using System.Globalization;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>
/// Reads the link of a reset e-mail, the one rule of what still sets a password: the page
/// checks it when it opens, and the reset checks it again when it is used.
/// </summary>
/// <remarks>
/// Reading uses nothing up: only a new password, by changing the security stamp the token
/// carries, makes the link unusable. The user manager follows the request's cancellation.
/// </remarks>
internal sealed class ResetLinks(UserManager<User> users)
{
    /// <summary>The link's account and token; null for a damaged, expired or used link.</summary>
    public async Task<ResetLink?> ReadAsync(int userId, string? encodedToken)
    {
        var token = EmailTokens.Decode(encodedToken);
        if (token is null)
        {
            return null;
        }

        var user = await users.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture));
        return user is not null && await IsValidAsync(user, token)
            ? new ResetLink(user, token)
            : null;
    }

    private Task<bool> IsValidAsync(User user, string token) =>
        users.VerifyUserTokenAsync(
            user,
            users.Options.Tokens.PasswordResetTokenProvider,
            UserManager<User>.ResetPasswordTokenPurpose,
            token);
}
