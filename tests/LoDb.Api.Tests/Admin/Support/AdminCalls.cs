using System.Net;
using LoDb.Api.Modules.Accounts.Security.Invitations;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Support;

/// <summary>What the admin front sends, and the administrators it sends it as.</summary>
public static class AdminCalls
{
    public const string Prefix = "/api/admin";
    public const string NewAdminName = "Nouvelle_Admin";
    public const string NewAdminEmail = "nouvelle.admin@example.test";

    /// <summary>A <c>DELETE</c> with the origin and anti-forgery token of the browser.</summary>
    public static async Task<HttpResponseMessage> DeleteAsync(BrowserClient browser, string path)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var request = browser.Post(path);
        request.Method = HttpMethod.Delete;
        return await browser.SendAsync(request);
    }

    /// <summary>
    /// An administrator without a second factor yet, signed in with their password: the
    /// state <c>admin create</c> leaves them in.
    /// </summary>
    public static Task<BrowserClient> OpenUnenrolledAsync(AccountsApp app) =>
        OpenUnenrolledAsync(
            app,
            new AccountSeed { Username = NewAdminName, Email = NewAdminEmail });

    /// <summary>
    /// <paramref name="admin"/>, an administrator without a second factor yet, signed in with
    /// their password.
    /// </summary>
    public static async Task<BrowserClient> OpenUnenrolledAsync(AccountsApp app, AccountSeed admin)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(admin);
        await app.SeedAsync(admin);
        await GrantAsync(app, admin.Username);
        return await SignedInAsync(app, admin.Username);
    }

    /// <summary>
    /// <paramref name="admin"/>, an administrator whose session was opened with a second
    /// factor, as the <c>Admin</c> policy asks.
    /// </summary>
    public static async Task<BrowserClient> OpenEnrolledAsync(AccountsApp app, AccountSeed admin)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(admin);
        await app.SeedAsync(admin);
        await GrantAsync(app, admin.Username);
        var secrets = await app.EnableTwoFactorAsync(admin.Username);
        var browser = app.Browser();
        using var signIn = await browser.PostAsync("/api/account/login", new
        {
            identifier = admin.Username,
            password = AccountSeed.StrongPassword,
            twoFactorCode = Totp.Code(secrets.Key, app.Clock),
        });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return browser;
    }

    /// <summary>A browser signed in with the password of a seeded account.</summary>
    public static async Task<BrowserClient> SignedInAsync(AccountsApp app, string username)
    {
        ArgumentNullException.ThrowIfNull(app);
        var browser = app.Browser();
        using var signIn = await browser.SignInAsync(username);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return browser;
    }

    /// <summary>
    /// The invitation <c>admin mfa-invite</c> would print for <paramref name="username"/>,
    /// issued by the host itself.
    /// </summary>
    public static async Task<string> InvitationAsync(AccountsApp app, string username)
    {
        ArgumentNullException.ThrowIfNull(app);
        var id = await IdOfAsync(app, username);
        return app.Services.GetRequiredService<MfaInvitations>().Issue(id);
    }

    /// <summary>The account id of <paramref name="username"/>.</summary>
    public static async Task<int> IdOfAsync(AccountsApp app, string username)
    {
        ArgumentNullException.ThrowIfNull(app);
        return (await app.FindAsync(username)).Id;
    }

    // The role row, created by the first administrator of the host, then the account in it.
    private static async Task GrantAsync(AccountsApp app, string username)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (!await roles.RoleExistsAsync(Role.Admin))
        {
            Assert.True((await roles.CreateAsync(new Role { Name = Role.Admin })).Succeeded);
        }

        var user = await users.FindByNameAsync(username);
        Assert.True((await users.AddToRoleAsync(user!, Role.Admin)).Succeeded);
    }
}
