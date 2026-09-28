using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The forgery guard of every unsafe <c>/api</c> call: a foreign <c>Origin</c> is refused,
/// and a call carried by the session cookie needs the XSRF token of that session.
/// </summary>
public sealed class ForgeryTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string LogoutPath = "/api/account/logout";
    private const string LoginPath = "/api/account/login";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost")]
    [InlineData("https://localhost.evil.example")]
    [InlineData("null")]
    public async Task ForeignOriginIsRefusedBeforeAnything(string origin)
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var request = browser.Post(LoginPath, Credentials());
        request.Headers.Remove(BrowserClient.OriginHeader);
        request.Headers.Add(BrowserClient.OriginHeader, origin);

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
        Assert.Empty(await App.AuditAsync());
    }

    [Fact]
    public async Task CallWithoutOriginIsNoBrowsersForgery()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var request = browser.Post(LoginPath, Credentials());
        request.Headers.Remove(BrowserClient.OriginHeader);

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SessionCallWithoutXsrfTokenIsRefused()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        using var request = browser.Post(LogoutPath);
        request.Headers.Remove(BrowserClient.XsrfHeader);

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "xsrf-invalid",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Equal(AccountSeed.Name, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task XsrfTokenOfTheAnonymousPageNoLongerCountsOnceSignedIn()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var page = await browser.GetAsync(BrowserClient.SessionPath);
        var anonymousToken = browser.Cookie(BrowserClient.XsrfCookie);
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
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
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        using var request = browser.Post(LogoutPath);
        request.Headers.Remove(BrowserClient.OriginHeader);
        request.Headers.Add(BrowserClient.OriginHeader, "https://evil.example");

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Equal(AccountSeed.Name, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task SafeCallsAreNeverRefused()
    {
        using var browser = App.Browser();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(BrowserClient.SessionPath, UriKind.Relative));
        request.Headers.Add(BrowserClient.OriginHeader, "https://evil.example");

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static object Credentials() =>
        new { identifier = AccountSeed.Name, password = AccountSeed.StrongPassword };
}
