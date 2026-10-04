using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Cli.Admin;

/// <summary>
/// Gives the <see cref="Role.Admin"/> role to the account of an e-mail, creating the account
/// and the role when they do not exist yet.
/// </summary>
/// <remarks>
/// Running it again changes nothing: an administrator stays one, and keeps their password
/// and their authenticator.
/// </remarks>
internal sealed partial class AdminGrant(
    UserManager<User> users,
    RoleManager<Role> roles,
    TimeProvider clock,
    ILogger<AdminGrant> logger)
{
    private const char EmailMark = '@';

    public async Task<AdminGrantResult> GrantAsync(string email, CancellationToken cancellation)
    {
        var password = default(string);
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            password = AdminPassword.Generate();
            user = await CreateAsync(email, password, cancellation);
        }

        var promoted = await PromoteAsync(user);
        return new AdminGrantResult
        {
            Username = user.UserName ?? string.Empty,
            Email = email,
            Password = password,
            Promoted = promoted,
            TwoFactorEnabled = user.TwoFactorEnabled,
        };
    }

    /// <summary>Gives the role to <paramref name="user"/>; false when it already had it.</summary>
    public async Task<bool> PromoteAsync(User user)
    {
        await EnsureRoleAsync();
        if (await users.IsInRoleAsync(user, Role.Admin))
        {
            return false;
        }

        Succeed(await users.AddToRoleAsync(user, Role.Admin), "grant the role");
        LogGranted(logger, user.Id);
        return true;
    }

    // The operator vouches for the address, so it counts as verified.
    private async Task<User> CreateAsync(string email, string password, CancellationToken token)
    {
        var username = await UsernameAllocator.AllocateAsync(
            [email.Split(EmailMark, 2)[0]],
            async (name, _) => await users.FindByNameAsync(name) is not null,
            token);
        var user = new User
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true,
            Roles = [],
            CreatedAt = clock.GetUtcNow(),
        };
        Succeed(await users.CreateAsync(user, password), "create the account");
        return user;
    }

    // The migration seeds no role: the first administrator brings it.
    private async Task EnsureRoleAsync()
    {
        if (!await roles.RoleExistsAsync(Role.Admin))
        {
            Succeed(await roles.CreateAsync(new Role { Name = Role.Admin }), "create the role");
        }
    }

    internal static void Succeed(IdentityResult result, string step)
    {
        if (!result.Succeeded)
        {
            var codes = string.Join(", ", result.Errors.Select(static error => error.Code));
            throw new InvalidOperationException($"Could not {step}: {codes}.");
        }
    }

    [LoggerMessage(
        EventName = "admin.granted",
        Level = LogLevel.Information,
        Message = "The admin role was granted to account {AccountId} from the shell.")]
    private static partial void LogGranted(ILogger logger, int accountId);
}
