using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Cli.Admin;

/// <summary>
/// Keeps the root administrator of a served host as its environment states it: the account of
/// an e-mail, with the username and the password the operator chose, holding the admin role.
/// </summary>
/// <remarks>
/// <para>
/// The legacy single admin (ADMIN_LOGIN, ADMIN_PASSWORD) carried over as an account. The
/// deploy job runs it at every deployment, so the environment stays the source of truth: a
/// password changed there is set again, which signs the account's sessions out.
/// </para>
/// <para>
/// The e-mail anchors the account and is never moved to another one; a username held by
/// another account stops the command. The authenticator, once enrolled, is kept: the admin
/// still asks for its code.
/// </para>
/// </remarks>
internal sealed class AdminRoot(UserManager<User> users, AdminGrant grant, TimeProvider clock)
{
    public async Task<AdminRootResult> EnsureAsync(AdminRootArguments root, string password)
    {
        var user = await users.FindByEmailAsync(root.Email);
        var created = user is null;
        user ??= await CreateAsync(root, password);
        if (!string.Equals(user.UserName, root.Username, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The account of {root.Email} is named {user.UserName}, not {root.Username}.");
        }

        var passwordChanged = !created && !await users.CheckPasswordAsync(user, password);
        if (passwordChanged)
        {
            // Hashed as given: Identity's validators are the CNIL policy for members.
            user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
            AdminGrant.Succeed(await users.UpdateSecurityStampAsync(user), "set the password");
        }

        return new AdminRootResult
        {
            Created = created,
            PasswordChanged = passwordChanged,
            Promoted = await grant.PromoteAsync(user),
        };
    }

    // The operator vouches for the address, so it counts as verified.
    private async Task<User> CreateAsync(AdminRootArguments root, string password)
    {
        if (await users.FindByNameAsync(root.Username) is not null)
        {
            throw new InvalidOperationException(
                $"The username {root.Username} belongs to another account.");
        }

        var user = new User
        {
            UserName = root.Username,
            Email = root.Email,
            EmailConfirmed = true,
            Roles = [],
            CreatedAt = clock.GetUtcNow(),
        };
        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        AdminGrant.Succeed(await users.CreateAsync(user), "create the account");
        return user;
    }
}
