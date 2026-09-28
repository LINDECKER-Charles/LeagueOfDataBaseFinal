using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Audit.Support;

/// <summary>
/// Browsers signed in on the accounts host: an administrator whose session was opened with a
/// second factor, as the <c>Admin</c> policy asks, or a plain account.
/// </summary>
public static class AdminBrowser
{
    public const string AdminName = "Operatrice_1";
    public const string AdminEmail = "operatrice@example.test";

    /// <summary>Creates the administrator, turns on its authenticator and signs it in.</summary>
    public static async Task<BrowserClient> OpenAsync(AccountsApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        await app.SeedAsync(new AccountSeed { Username = AdminName, Email = AdminEmail });
        await GrantAdminAsync(app, AdminName);
        var secrets = await app.EnableTwoFactorAsync(AdminName);
        var browser = app.Browser();
        using var signIn = await browser.PostAsync(
            "/api/account/login",
            new
            {
                identifier = AdminName,
                password = AccountSeed.StrongPassword,
                twoFactorCode = Totp.Code(secrets.Key),
            });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return browser;
    }

    /// <summary>A signed-in account without the Admin role.</summary>
    public static async Task<BrowserClient> OpenMemberAsync(AccountsApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        await app.SeedAsync(new AccountSeed());
        var browser = app.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return browser;
    }

    // As the admin lot will: the role row, then the account in it.
    private static async Task GrantAdminAsync(AccountsApp app, string username)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var created = await roles.CreateAsync(new Role { Name = Role.Admin });
        var granted = await users.AddToRoleAsync(
            (await users.FindByNameAsync(username))!,
            Role.Admin);
        Assert.True(created.Succeeded && granted.Succeeded);
    }
}
