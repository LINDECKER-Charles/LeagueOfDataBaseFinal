using System.Net;
using LoDb.Api.Tests.Accounts.Support;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The forgery guard of every unsafe <c>/api</c> call: a foreign <c>Origin</c> is refused,
/// and a call carried by the session cookie needs the XSRF token of that session.
/// </summary>
/// <remarks>The tests share a host, each with an account of its own.</remarks>
public sealed class ForgeryTests(SharedAccountsApp host) : IClassFixture<SharedAccountsApp>
{
    private const string LogoutPath = "/api/account/logout";
    private const string LoginPath = "/api/account/login";
    private const string ForeignOrigin = "https://evil.example";

    private readonly AccountSeed _account = host.Unique();

    private AccountsApp App { get; } = host.App;

    [Fact]
    public async Task ForeignOriginIsRefusedBeforeAnything()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var request = browser.Post(LoginPath, Credentials());
        request.Headers.Remove(BrowserClient.OriginHeader);
        request.Headers.Add(BrowserClient.OriginHeader, ForeignOrigin);

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
        Assert.Empty(await App.AuditOfAsync(_account.Username));
    }

    [Fact]
    public async Task CallWithoutOriginIsNoBrowsersForgery()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var request = browser.Post(LoginPath, Credentials());
        request.Headers.Remove(BrowserClient.OriginHeader);

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SessionCallWithoutXsrfTokenIsRefused()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(_account.Username);
        using var request = browser.Post(LogoutPath);
        request.Headers.Remove(BrowserClient.XsrfHeader);

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "xsrf-invalid",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Equal(_account.Username, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task XsrfTokenOfTheAnonymousPageNoLongerCountsOnceSignedIn()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var page = await browser.GetAsync(BrowserClient.SessionPath);
        var anonymousToken = browser.Cookie(BrowserClient.XsrfCookie);
        using var signIn = await browser.SignInAsync(_account.Username);
        using var request = browser.Post(LogoutPath);
        request.Headers.Remove(BrowserClient.XsrfHeader);
        request.Headers.Add(BrowserClient.XsrfHeader, anonymousToken);

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "xsrf-invalid",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task SessionCallFromAForeignOriginIsRefusedEvenWithItsToken()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(_account.Username);
        using var request = browser.Post(LogoutPath);
        request.Headers.Remove(BrowserClient.OriginHeader);
        request.Headers.Add(BrowserClient.OriginHeader, ForeignOrigin);

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Equal(_account.Username, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task BearerHeaderThatAuthenticatesNothingExemptsNothing()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var request = browser.Post(LoginPath, Credentials());
        request.Headers.Remove(BrowserClient.OriginHeader);
        request.Headers.Add(BrowserClient.OriginHeader, ForeignOrigin);
        request.Headers.Authorization = new("Bearer", "forged");

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    [Fact]
    public async Task SafeCallsAreNeverRefused()
    {
        using var browser = App.Browser();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(BrowserClient.SessionPath, UriKind.Relative));
        request.Headers.Add(BrowserClient.OriginHeader, ForeignOrigin);

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private object Credentials() =>
        new { identifier = _account.Username, password = AccountSeed.StrongPassword };
}
