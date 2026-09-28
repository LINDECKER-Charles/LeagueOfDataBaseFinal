using System.Net;
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
    public static async Task<BrowserClient> OpenUnenrolledAsync(AccountsApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        await app.SeedAsync(new AccountSeed { Username = NewAdminName, Email = NewAdminEmail });
        await GrantAsync(app, NewAdminName);
        var browser = app.Browser();
        using var signIn = await browser.SignInAsync(NewAdminName);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return browser;
    }

    /// <summary>The account id of <paramref name="username"/>.</summary>
    public static async Task<int> IdOfAsync(AccountsApp app, string username)
    {
        ArgumentNullException.ThrowIfNull(app);
        return (await app.FindAsync(username)).Id;
    }

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
