using System.Security.Claims;
using System.Text.Json;
using LoDb.Api.Hosting;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The named policies, evaluated and answered as the authorization middleware does, for the
/// principal of a real session: <c>Authenticated</c>, <c>VerifiedEmail</c> against the
/// account as stored now, and <c>Admin</c>, which also needs a second factor.
/// </summary>
/// <remarks>The tests share a host, each with an account of its own.</remarks>
public sealed class AuthorizationPoliciesTests(SharedAccountsApp host)
    : IClassFixture<SharedAccountsApp>
{
    private const string ApiPath = "/api/account/anything";
    private const int Allowed = StatusCodes.Status200OK;

    private readonly AccountSeed _account = host.Unique();

    private AccountsApp App { get; } = host.App;

    [Fact]
    public async Task VerifiedEmailIsReadFromTheAccountAsStoredNow()
    {
        await App.SeedAsync(_account with { EmailConfirmed = false });
        var session = await SessionAsync(_account.Username);

        var before = await AuthorizeAsync(AuthorizationPolicies.VerifiedEmail, session);
        var signedIn = await AuthorizeAsync(AuthorizationPolicies.Authenticated, session);
        await App.UsersAsync(async users =>
        {
            var user = await users.FindByNameAsync(_account.Username);
            user!.EmailConfirmed = true;
            return await users.UpdateAsync(user);
        });
        var after = await AuthorizeAsync(AuthorizationPolicies.VerifiedEmail, session);

        Assert.Equal((StatusCodes.Status403Forbidden, "email-not-verified"), before);
        Assert.Equal((Allowed, (string?)null), signedIn);
        Assert.Equal((Allowed, (string?)null), after);
    }

    [Fact]
    public async Task BannedAccountIsRefusedBeforeItsSessionIsRevalidated()
    {
        await App.SeedAsync(_account);
        var session = await SessionAsync(_account.Username);
        await App.BanAsync(_account.Username);

        var answer = await AuthorizeAsync(AuthorizationPolicies.VerifiedEmail, session);

        Assert.Equal((StatusCodes.Status403Forbidden, "forbidden"), answer);
    }

    [Fact]
    public async Task AdministratorNeedsASessionOpenedWithASecondFactor()
    {
        await App.SeedAsync(_account);
        await GrantAdminAsync(_account.Username);
        var passwordOnly = await SessionAsync(_account.Username);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        var withCode = await SessionAsync(_account.Username, Totp.Code(secrets.Key, App.Clock));

        var refused = await AuthorizeAsync(AuthorizationPolicies.Admin, passwordOnly);
        var allowed = await AuthorizeAsync(AuthorizationPolicies.Admin, withCode);

        Assert.Equal((StatusCodes.Status403Forbidden, "mfa-required"), refused);
        Assert.Equal((Allowed, (string?)null), allowed);
    }

    [Fact]
    public async Task SecondFactorMakesNoAdministrator()
    {
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        var withCode = await SessionAsync(_account.Username, Totp.Code(secrets.Key, App.Clock));

        var answer = await AuthorizeAsync(AuthorizationPolicies.Admin, withCode);

        Assert.Equal((StatusCodes.Status403Forbidden, "forbidden"), answer);
    }

    // As the admin lot will: the role row, created by the first test that needs it, then the
    // account in it.
    private async Task GrantAdminAsync(string username)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (!await roles.RoleExistsAsync(Role.Admin))
        {
            Assert.True((await roles.CreateAsync(new Role { Name = Role.Admin })).Succeeded);
        }

        var granted = await users.AddToRoleAsync(
            (await users.FindByNameAsync(username))!,
            Role.Admin);

        Assert.True(granted.Succeeded);
    }

    // The principal the session cookie of a browser sign-in carries.
    private async Task<ClaimsPrincipal> SessionAsync(string username, string? twoFactorCode = null)
    {
        using var browser = App.Browser();
        using var signIn = await browser.PostAsync(
            "/api/account/login",
            new { identifier = username, password = AccountSeed.StrongPassword, twoFactorCode });
        Assert.Equal(StatusCodes.Status200OK, (int)signIn.StatusCode);
        var cookie = App.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var ticket = cookie.TicketDataFormat.Unprotect(browser.Cookie(BrowserClient.SessionCookie));
        return ticket?.Principal ?? throw new InvalidOperationException("No session.");
    }

    // The policy evaluated, then answered, as the authorization middleware does for /api.
    private async Task<(int Status, string? Code)> AuthorizeAsync(
        string policyName,
        ClaimsPrincipal user)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = new DefaultHttpContext { RequestServices = services, User = user };
        // A safe call: the forgery guard, checked by HTTP, stays out of the verdict.
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = ApiPath;
        context.Response.Body = new MemoryStream();
        var policy = await services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(policyName);
        var authenticated = user.Identity?.IsAuthenticated == true
            ? AuthenticateResult.Success(new AuthenticationTicket(user, "session"))
            : AuthenticateResult.NoResult();
        var result = await services.GetRequiredService<IPolicyEvaluator>()
            .AuthorizeAsync(policy!, authenticated, context, context);
        await services.GetRequiredService<IAuthorizationMiddlewareResultHandler>()
            .HandleAsync(static _ => Task.CompletedTask, context, policy!, result);
        return (context.Response.StatusCode, await CodeAsync(context.Response));
    }

    private static async Task<string?> CodeAsync(HttpResponse response)
    {
        if (response.Body.Length == 0)
        {
            return null;
        }

        response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(
            response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        return problem.RootElement.GetProperty("code").GetString();
    }
}
