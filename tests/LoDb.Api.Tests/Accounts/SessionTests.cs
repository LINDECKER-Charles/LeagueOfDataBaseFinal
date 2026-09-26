using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The session cookie: <c>__Host-</c>, HttpOnly, Secure and Lax, one day without "remember
/// me" and thirty with it, closed by a sign-out, and by a ban at its next revalidation.
/// </summary>
public sealed class SessionTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private static readonly TimeSpan PastRevalidation = TimeSpan.FromMinutes(6);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task AnonymousSessionIsNotStoredAndGetsAReadableXsrfToken()
    {
        using var browser = App.Browser();

        using var response = await browser.GetAsync(BrowserClient.SessionPath);

        var session = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, session.GetProperty("user").ValueKind);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var xsrf = SetCookie(response, BrowserClient.XsrfCookie);
        Assert.Equal(
            (false, true, SameSiteMode.Strict, "/"),
            (xsrf.HttpOnly, xsrf.Secure, xsrf.SameSite, xsrf.Path.Value));
        Assert.False(string.IsNullOrEmpty(browser.Cookie(BrowserClient.XsrfCookie)));
    }

    [Fact]
    public async Task SessionCookieIsHostOnlyHttpOnlySecureAndLax()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();

        using var response = await browser.SignInAsync(AccountSeed.Name);

        var session = SetCookie(response, BrowserClient.SessionCookie);
        Assert.Equal(
            (true, true, SameSiteMode.Lax, "/", null),
            (session.HttpOnly, session.Secure, session.SameSite, session.Path.Value,
                session.Domain.Value));
        Assert.Null(session.Expires);
    }

    [Fact]
    public async Task RememberMeKeepsTheSessionThirtyDaysAndOtherwiseADay()
    {
        await App.SeedAsync(new AccountSeed());
        using var remembered = App.Browser();
        using var browsing = App.Browser();
        var signedInAt = App.Clock.GetUtcNow();

        using var rememberedSignIn = await remembered.SignInAsync(
            AccountSeed.Name,
            rememberMe: true);
        using var browsingSignIn = await browsing.SignInAsync(AccountSeed.Name);
        var expires = SetCookie(rememberedSignIn, BrowserClient.SessionCookie).Expires;
        App.Clock.Advance(TimeSpan.FromHours(25));
        var afterADay = (await remembered.SignedInAsAsync(), await browsing.SignedInAsAsync());
        App.Clock.Advance(TimeSpan.FromDays(31));

        Assert.InRange(
            expires!.Value,
            signedInAt.AddDays(30).AddMinutes(-1),
            signedInAt.AddDays(30).AddMinutes(1));
        Assert.Equal((AccountSeed.Name, (string?)null), afterADay);
        Assert.Null(await remembered.SignedInAsAsync());
    }

    [Fact]
    public async Task SignOutClosesTheSessionAndIsAudited()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        var signedInToken = browser.Cookie(BrowserClient.XsrfCookie);

        using var signOut = await browser.PostAsync("/api/account/logout");

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
        Assert.NotEqual(signedInToken, browser.Cookie(BrowserClient.XsrfCookie));
        Assert.Null(await browser.SignedInAsAsync());
        var entry = (await App.AuditAsync())[^1];
        Assert.Equal(
            (AuditAction.UserLogout, AuditActorType.User, (int?)account.Id),
            (entry.Action, entry.ActorType, entry.ActorId));
    }

    [Fact]
    public async Task SignOutNeedsASession()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync("/api/account/logout");

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task BanEndsAnOpenSessionThenItsCookieAtItsNextRevalidation()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name, rememberMe: true);

        await App.BanAsync(AccountSeed.Name);
        var rightAfterTheBan = await browser.SignedInAsAsync();
        var cookieBeforeRevalidation = browser.Cookie(BrowserClient.SessionCookie);
        App.Clock.Advance(PastRevalidation);
        var afterRevalidation = await browser.SignedInAsAsync();

        Assert.Null(rightAfterTheBan);
        Assert.NotNull(cookieBeforeRevalidation);
        Assert.Null(afterRevalidation);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    [Fact]
    public async Task RevalidatedSessionKeepsItsSecondFactor()
    {
        await App.SeedAsync(new AccountSeed());
        var secrets = await App.EnableTwoFactorAsync(AccountSeed.Name);
        using var browser = App.Browser();
        using var signIn = await browser.PostAsync("/api/account/login", new
        {
            identifier = AccountSeed.Name,
            password = AccountSeed.StrongPassword,
            twoFactorCode = Totp.Code(secrets.Key),
        });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        App.Clock.Advance(PastRevalidation);
        var user = await browser.SessionUserAsync();

        Assert.True(user?.GetProperty("multiFactor").GetBoolean());
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static SetCookieHeaderValue SetCookie(HttpResponseMessage response, string name)
    {
        var headers = response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? values
            : [];
        return Assert.Single(
            SetCookieHeaderValue.ParseList([.. headers]),
            cookie => cookie.Name.Equals(name, StringComparison.Ordinal));
    }
}
