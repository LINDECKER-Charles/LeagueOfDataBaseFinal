using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The session cookie: <c>__Host-</c>, HttpOnly, Secure and Lax, closed by a sign-out, which
/// like the sign-out everywhere needs a session. What time does to it is
/// <see cref="OwnHost.SessionLifetimeTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with an account of its own.</remarks>
public sealed class SessionTests(SharedAccountsApp host) : IClassFixture<SharedAccountsApp>
{
    private readonly AccountSeed _account = host.Unique();

    private AccountsApp App { get; } = host.App;

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
        await App.SeedAsync(_account);
        using var browser = App.Browser();

        using var response = await browser.SignInAsync(_account.Username);

        var session = SetCookie(response, BrowserClient.SessionCookie);
        Assert.Equal(
            (true, true, SameSiteMode.Lax, "/", null),
            (session.HttpOnly, session.Secure, session.SameSite, session.Path.Value,
                session.Domain.Value));
        Assert.Null(session.Expires);
    }

    [Fact]
    public async Task SignOutClosesTheSessionAndIsAudited()
    {
        var account = await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(_account.Username);
        var signedInToken = browser.Cookie(BrowserClient.XsrfCookie);

        using var signOut = await browser.PostAsync("/api/account/logout");

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
        Assert.NotEqual(signedInToken, browser.Cookie(BrowserClient.XsrfCookie));
        Assert.Null(await browser.SignedInAsAsync());
        var entry = (await App.AuditOfAsync(_account.Username))[^1];
        Assert.Equal(
            (AuditAction.UserLogout, AuditActorType.User, (int?)account.Id),
            (entry.Action, entry.ActorType, entry.ActorId));
    }

    // The XSRF token their policy asks for as well is ForgeryTests'.
    [Theory]
    [InlineData("/api/account/logout")]
    [InlineData("/api/account/logout-everywhere")]
    public async Task SignOutsNeedASession(string path)
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(path);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Unauthorized));
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
